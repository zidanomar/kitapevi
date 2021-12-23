using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using KitapEvi.DataAccess.Repository.IRepository;
using KitapEvi.Models;
using KitapEvi.Models.ViewModels;
using KitapEvi.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;

namespace KitapEvi.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class OrderController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        [BindProperty]
        public OrderDetailsVM OrderVM { get; set; }
        public OrderController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Details(int id)
        {
            OrderVM = new OrderDetailsVM()
            {
                OrderHeader = _unitOfWork.OrderHeader.GetFirstOrDefault(u => u.Id == id,
                                                includeProperties: "ApplicationUser"),
                OrderDetails = _unitOfWork.OrderDetails.GetAll(o => o.OrderId == id, includeProperties: "Product")

            };
            return View(OrderVM);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Details")]
        public IActionResult Details(string stripeToken)
        {
            OrderHeader orderHeader = _unitOfWork.OrderHeader.GetFirstOrDefault(u => u.Id == OrderVM.OrderHeader.Id,
                                                includeProperties: "ApplicationUser");
            if (stripeToken != null)
            {
                //process the payment
                var options = new ChargeCreateOptions
                {
                    Amount = Convert.ToInt32(orderHeader.OrderTotal * 100),
                    Currency = "usd",
                    Description = "Order ID : " + orderHeader.Id,
                    Source = stripeToken
                };

                var service = new ChargeService();
                Charge charge = service.Create(options);

                if (charge.Id == null)
                {
                    orderHeader.PaymentStatus = SharedDetail.PaymentStatusRejected;
                }
                else
                {
                    orderHeader.TransactionId = charge.Id;
                }
                if (charge.Status.ToLower() == "succeeded")
                {
                    orderHeader.PaymentStatus = SharedDetail.PaymentStatusApproved;

                    orderHeader.PaymentDate = DateTime.Now;
                }

                _unitOfWork.Save();

            }
            return RedirectToAction("Details", "Order", new { id = orderHeader.Id });
        }


        [Authorize(Roles = SharedDetail.Role_Admin + "," + SharedDetail.Role_Employee)]
        public IActionResult StartProcessing(int id)
        {
            OrderHeader orderHeader = _unitOfWork.OrderHeader.GetFirstOrDefault(u => u.Id == id);
            orderHeader.OrderStatus = SharedDetail.StatusInProcess;
            _unitOfWork.Save();
            return RedirectToAction("Index");
        }

        [HttpPost]
        [Authorize(Roles = SharedDetail.Role_Admin + "," + SharedDetail.Role_Employee)]
        public IActionResult ShipOrder()
        {
            OrderHeader orderHeader = _unitOfWork.OrderHeader.GetFirstOrDefault(u => u.Id == OrderVM.OrderHeader.Id);
            orderHeader.TrackingNumber = OrderVM.OrderHeader.TrackingNumber;
            orderHeader.Carrier = OrderVM.OrderHeader.Carrier;
            orderHeader.OrderStatus = SharedDetail.StatusShipped;
            orderHeader.ShippingDate = DateTime.Now;

            _unitOfWork.Save();
            return RedirectToAction("Index");
        }

        [Authorize(Roles = SharedDetail.Role_Admin + "," + SharedDetail.Role_Employee)]
        public IActionResult CancelOrder(int id)
        {
            OrderHeader orderHeader = _unitOfWork.OrderHeader.GetFirstOrDefault(u => u.Id == id);
            if (orderHeader.PaymentStatus == SharedDetail.StatusApproved)
            {
                var options = new RefundCreateOptions
                {
                    Amount = Convert.ToInt32(orderHeader.OrderTotal * 100),
                    Reason = RefundReasons.RequestedByCustomer,
                    Charge = orderHeader.TransactionId

                };
                var service = new RefundService();
                Refund refund = service.Create(options);

                orderHeader.OrderStatus = SharedDetail.StatusRefunded;
                orderHeader.PaymentStatus = SharedDetail.StatusRefunded;
            }
            else
            {
                orderHeader.OrderStatus = SharedDetail.StatusCancelled;
                orderHeader.PaymentStatus = SharedDetail.StatusCancelled;
            }

            _unitOfWork.Save();
            return RedirectToAction("Index");
        }

        public IActionResult UpdateOrderDetails()
        {
            var orderHEaderFromDb = _unitOfWork.OrderHeader.GetFirstOrDefault(u => u.Id == OrderVM.OrderHeader.Id);
            orderHEaderFromDb.Name = OrderVM.OrderHeader.Name;
            orderHEaderFromDb.PhoneNumber = OrderVM.OrderHeader.PhoneNumber;
            orderHEaderFromDb.StreetAddress = OrderVM.OrderHeader.StreetAddress;
            orderHEaderFromDb.City = OrderVM.OrderHeader.City;
            orderHEaderFromDb.State = OrderVM.OrderHeader.State;
            orderHEaderFromDb.PostalCode = OrderVM.OrderHeader.PostalCode;
            if (OrderVM.OrderHeader.Carrier != null)
            {
                orderHEaderFromDb.Carrier = OrderVM.OrderHeader.Carrier;
            }
            if (OrderVM.OrderHeader.TrackingNumber != null)
            {
                orderHEaderFromDb.TrackingNumber = OrderVM.OrderHeader.TrackingNumber;
            }

            _unitOfWork.Save();
            TempData["Error"] = "Order Details Updated Successfully.";
            return RedirectToAction("Details", "Order", new { id = orderHEaderFromDb.Id });
        }


        #region API CALLS
        [HttpGet]
        public IActionResult GetOrderList(string status)
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

            IEnumerable<OrderHeader> orderHeaderList;

            if (User.IsInRole(SharedDetail.Role_Admin) || User.IsInRole(SharedDetail.Role_Employee))
            {
                orderHeaderList = _unitOfWork.OrderHeader.GetAll(includeProperties: "ApplicationUser");
            }
            else
            {
                orderHeaderList = _unitOfWork.OrderHeader.GetAll(
                                        u => u.ApplicationUserId == claim.Value,
                                        includeProperties: "ApplicationUser");
            }

            switch (status)
            {
                case "pending":
                    orderHeaderList = orderHeaderList.Where(o => o.PaymentStatus == SharedDetail.PaymentStatusDelayedPayment);
                    break;
                case "inprocess":
                    orderHeaderList = orderHeaderList.Where(o => o.OrderStatus == SharedDetail.StatusApproved ||
                                                            o.OrderStatus == SharedDetail.StatusInProcess ||
                                                            o.OrderStatus == SharedDetail.StatusPending);
                    break;
                case "completed":
                    orderHeaderList = orderHeaderList.Where(o => o.OrderStatus == SharedDetail.StatusShipped);
                    break;
                case "rejected":
                    orderHeaderList = orderHeaderList.Where(o => o.OrderStatus == SharedDetail.StatusCancelled ||
                                                            o.OrderStatus == SharedDetail.StatusRefunded ||
                                                            o.OrderStatus == SharedDetail.PaymentStatusRejected);
                    break;
                default:
                    break;
            }

            return Json(new { data = orderHeaderList });
        }
        #endregion
    }
}
//using KitapEvi.DataAccess.Repository.IRepository;
//using KitapEvi.Models;
//using KitapEvi.Models.ViewModels;
//using KitapEvi.Utility;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using Stripe;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Security.Claims;
//using System.Threading.Tasks;

//namespace KitapEvi.Areas.Admin.Controllers
//{
//	[Area("Admin")]
//	public class OrderController : Controller
//	{
//		private readonly IUnitOfWork _unitOfWork;
//		public OrderDetailsVM OrderVM { get; set; }
//		public OrderController(IUnitOfWork unitOfWork)
//		{
//			_unitOfWork = unitOfWork;
//		}
//		public IActionResult Index()
//		{
//			return View();
//		}

//		public IActionResult Details(int id)
//		{
//			OrderVM = new OrderDetailsVM()
//			{
//				OrderHeader = _unitOfWork.OrderHeader.GetFirstOrDefault(u => u.Id == id,
//												includeProperties: "ApplicationUser"),
//				OrderDetails = _unitOfWork.OrderDetails.GetAll(o => o.OrderId == id, includeProperties: "Product")

//			};
//			return View(OrderVM);
//		}

//		[Authorize(Roles = SharedDetail.Role_Admin + "," + SharedDetail.Role_Employee)]
//		public IActionResult StartProcessing(int id)
//		{
//			OrderHeader orderHeader = _unitOfWork.OrderHeader.GetFirstOrDefault(u => u.Id == id);
//			orderHeader.OrderStatus = SharedDetail.StatusInProcess;
//			_unitOfWork.Save();
//			return RedirectToAction("Index");
//		}

//		[HttpPost]
//		[Authorize(Roles = SharedDetail.Role_Admin + "," + SharedDetail.Role_Employee)]
//		public IActionResult ShipOrder()
//		{
//			OrderHeader orderHeader = _unitOfWork.OrderHeader.GetFirstOrDefault(u => u.Id == OrderVM.OrderHeader.Id);
//			orderHeader.TrackingNumber = OrderVM.OrderHeader.TrackingNumber;
//			orderHeader.Carrier = OrderVM.OrderHeader.Carrier;
//			orderHeader.OrderStatus = SharedDetail.StatusShipped;
//			orderHeader.ShippingDate = DateTime.Now;

//			_unitOfWork.Save();
//			return RedirectToAction("Index");
//		}

//		[Authorize(Roles = SharedDetail.Role_Admin + "," + SharedDetail.Role_Employee)]
//		public IActionResult CancelOrder(int id)
//		{
//			OrderHeader orderHeader = _unitOfWork.OrderHeader.GetFirstOrDefault(u => u.Id == id);
//			if (orderHeader.PaymentStatus == SharedDetail.StatusApproved)
//			{
//				var options = new RefundCreateOptions
//				{
//					Amount = Convert.ToInt32(orderHeader.OrderTotal * 100),
//					Reason = RefundReasons.RequestedByCustomer,
//					Charge = orderHeader.TransactionId

//				};
//				var service = new RefundService();
//				Refund refund = service.Create(options);

//				orderHeader.OrderStatus = SharedDetail.StatusRefunded;
//				orderHeader.PaymentStatus = SharedDetail.StatusRefunded;
//			}
//			else
//			{
//				orderHeader.OrderStatus = SharedDetail.StatusCancelled;
//				orderHeader.PaymentStatus = SharedDetail.StatusCancelled;
//			}

//			_unitOfWork.Save();
//			return RedirectToAction("Index");
//		}

//		[HttpPost]
//		[ValidateAntiForgeryToken]
//		[ActionName("Details")]
//		public IActionResult Details(string stripeToken)
//		{
//			OrderHeader orderHeader = _unitOfWork.OrderHeader.GetFirstOrDefault(u => u.Id == OrderVM.OrderHeader.Id,
//												includeProperties: "ApplicationUser");
//			if (stripeToken != null)
//			{
//				//process the payment
//				var options = new ChargeCreateOptions
//				{
//					Amount = Convert.ToInt32(orderHeader.OrderTotal * 100),
//					Currency = "uSharedDetail",
//					Description = "Order ID : " + orderHeader.Id,
//					Source = stripeToken
//				};

//				var service = new ChargeService();
//				Charge charge = service.Create(options);

//				if (charge.Id == null)
//				{
//					orderHeader.PaymentStatus = SharedDetail.PaymentStatusRejected;
//				}
//				else
//				{
//					orderHeader.TransactionId = charge.Id;
//				}
//				if (charge.Status.ToLower() == "succeeded")
//				{
//					orderHeader.PaymentStatus = SharedDetail.PaymentStatusApproved;

//					orderHeader.PaymentDate = DateTime.Now;
//				}

//				_unitOfWork.Save();

//			}
//			return RedirectToAction("Details", "Order", new { id = orderHeader.Id });
//		}

//		[HttpGet]
//		public IActionResult GetOrderList(string status)
//		{
//			var claimsIdentity = (ClaimsIdentity)User.Identity;
//			var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

//			IEnumerable<OrderHeader> orderHeaderList;

//			orderHeaderList = _unitOfWork.OrderHeader.GetAll(includeProperties: "ApplicationUser");

//			if (User.IsInRole(SharedDetail.Role_Admin) || User.IsInRole(SharedDetail.Role_Employee))
//			{
//				orderHeaderList = _unitOfWork.OrderHeader.GetAll(includeProperties: "ApplicationUser");
//			}
//			else
//			{
//				orderHeaderList = _unitOfWork.OrderHeader.GetAll(
//										u => u.ApplicationUserId == claim.Value,
//										includeProperties: "ApplicationUser");
//			}

//			switch (status)
//			{
//				case "pending":
//					orderHeaderList = orderHeaderList.Where(o => o.PaymentStatus == SharedDetail.PaymentStatuSharedDetailelayedPayment);
//					break;
//				case "inprocess":
//					orderHeaderList = orderHeaderList.Where(o => o.OrderStatus == SharedDetail.StatusApproved ||
//															o.OrderStatus == SharedDetail.StatusInProcess ||
//															o.OrderStatus == SharedDetail.StatusPending);
//					break;
//				case "completed":
//					orderHeaderList = orderHeaderList.Where(o => o.OrderStatus == SharedDetail.StatusShipped);
//					break;
//				case "rejected":
//					orderHeaderList = orderHeaderList.Where(o => o.OrderStatus == SharedDetail.StatusCancelled ||
//															o.OrderStatus == SharedDetail.StatusRefunded ||
//															o.OrderStatus == SharedDetail.PaymentStatusRejected);
//					break;
//				default:
//					break;
//			}

//			return Json(new { data = orderHeaderList });
//		}
//	}
//}
