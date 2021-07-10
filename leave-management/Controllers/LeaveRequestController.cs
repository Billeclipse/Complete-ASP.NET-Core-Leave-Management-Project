using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using AutoMapper;
using leave_management.Contracts;
using leave_management.Data;
using leave_management.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace leave_management.Controllers
{
    [Authorize]
    public class LeaveRequestController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly UserManager<Employee> _userManager;
        
        public LeaveRequestController(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            UserManager<Employee> userManager)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _userManager = userManager;
        }

        [Authorize(Roles = "Administrator")]
        // GET: LeaveRequestController
        public async Task<ActionResult> Index()
        {
            var leaveRequests = await _unitOfWork.LeaveRequests.FindAll(
                includes: q => q.Include(x=> x.RequestingEmployee).Include(x=> x.LeaveType));
            var leaveRequestsModels = _mapper.Map<List<LeaveRequestVM>>(leaveRequests);
            var model = new AdminLeaveRequestViewVM()
            {
                TotalRequests = leaveRequestsModels.Count,
                ApprovedRequests = leaveRequestsModels.Count(q => q.Approved == true),
                PendingRequests = leaveRequestsModels.Count(q => q.Approved == null),
                RejectedRequests = leaveRequestsModels.Count(q => q.Approved == false),
                CancelledRequests = leaveRequestsModels.Count(q => q.Cancelled == true),
                LeaveRequests = leaveRequestsModels
            };

            return View(model);
        }

        public async Task<ActionResult> MyLeave()
        {
            var employee = await _userManager.GetUserAsync(User);
            var leaveAllocations = await _unitOfWork.LeaveAllocations.FindAll(q=>q.EmployeeId == employee.Id,
                includes: q => q.Include(x => x.LeaveType));
            var leaveAllocationsModels = _mapper.Map<List<LeaveAllocationVM>>(leaveAllocations);
            
            var leaveRequests = await _unitOfWork.LeaveRequests.FindAll(q=> q.RequestingEmployeeId == employee.Id);
            var leaveRequestsModels = _mapper.Map<List<LeaveRequestVM>>(leaveRequests);

            var model = new EmployeeLeaveRequestViewVM()
            {
                LeaveRequests = leaveRequestsModels,
                LeaveAllocations = leaveAllocationsModels
            };

            return View(model);
        }

        // GET: LeaveRequestController/Details/5
        public async Task<ActionResult> Details(int id)
        {
            var leaveRequest = await _unitOfWork.LeaveRequests.Find(q=> q.Id == id,
                includes: q => q.Include(x => x.ApprovedBy).Include(x => x.RequestingEmployee).Include(x => x.LeaveType));
            var model = _mapper.Map<LeaveRequestVM>(leaveRequest);
            return View(model);
        }

        public async Task<ActionResult> ApproveRequest(int id)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var leaveRequest = await _unitOfWork.LeaveRequests.Find(q=> q.Id == id);
                var employeeId = leaveRequest.RequestingEmployeeId;
                var leaveTypeId = leaveRequest.LeaveTypeId;
                var leaveAllocation = await _unitOfWork.LeaveAllocations.Find(q => q.EmployeeId == employeeId
                                                                              && q.Period == DateTime.Now.Year
                                                                              && q.LeaveTypeId == leaveTypeId);
                var daysRequested = (int)(leaveRequest.EndDate - leaveRequest.StartDate).TotalDays;
                leaveAllocation.NumberOfDays -= daysRequested;

                leaveRequest.Approved = true;
                leaveRequest.ApprovedById = user.Id;
                leaveRequest.DateActioned = DateTime.Now;
                
                _unitOfWork.LeaveRequests.Update(leaveRequest);
                _unitOfWork.LeaveAllocations.Update(leaveAllocation);
                await _unitOfWork.Save();

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return RedirectToAction(nameof(Index));
            }
        }

        public async Task<ActionResult> RejectRequest(int id)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var leaveRequest = await _unitOfWork.LeaveRequests.Find(q=> q.Id == id);
                leaveRequest.Approved = false;
                leaveRequest.ApprovedById = user.Id;
                leaveRequest.DateActioned = DateTime.Now;
                _unitOfWork.LeaveRequests.Update(leaveRequest);
                await _unitOfWork.Save();

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: LeaveRequestController/Create
        public async Task<ActionResult> Create()
        {
            var leaveTypes = await _unitOfWork.LeaveTypes.FindAll();
            var leaveTypeItems = leaveTypes.Select(q => new SelectListItem
            {
                Text = q.Name,
                Value = q.Id.ToString()
            });
            var model = new CreateLeaveRequestVM
            {
                LeaveTypes = leaveTypeItems
            };
            return View(model);
        }

        // POST: LeaveRequestController/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(CreateLeaveRequestVM model)
        {
            try
            {
                var startDate = Convert.ToDateTime(model.StartDate);
                var endDate = Convert.ToDateTime(model.EndDate);
                var leaveTypes = await _unitOfWork.LeaveTypes.FindAll();
                var leaveTypeItems = leaveTypes.Select(q => new SelectListItem
                {
                    Text = q.Name,
                    Value = q.Id.ToString()
                });
                model.LeaveTypes = leaveTypeItems;
                if (!ModelState.IsValid)
                {
                    return View(model);
                }

                if (DateTime.Compare(startDate, endDate) > 1)
                {
                    ModelState.AddModelError("", "Start Date cannot be further in the future than the End Date");
                    return View(model);
                }

                var employee = await _userManager.GetUserAsync(User);
                var leaveAllocation = await _unitOfWork.LeaveAllocations.Find(q => q.EmployeeId == employee.Id
                                                                                && q.Period == DateTime.Now.Year
                                                                                && q.LeaveTypeId == model.LeaveTypeId);
                var daysRequested = (int)(endDate - startDate).TotalDays;

                if (daysRequested > leaveAllocation.NumberOfDays)
                {
                    ModelState.AddModelError("", "You do not have sufficient days for this request");
                    return View(model);
                }

                var leaveRequestModel = new LeaveRequestVM
                {
                    RequestingEmployeeId = employee.Id,
                    StartDate = startDate,
                    EndDate = endDate,
                    Approved = null,
                    DateRequested = DateTime.Now,
                    DateActioned = DateTime.Now,
                    LeaveTypeId = model.LeaveTypeId,
                    RequestComments = model.RequestComments
                };

                var leaveRequest = _mapper.Map<LeaveRequest>(leaveRequestModel);
                await _unitOfWork.LeaveRequests.Create(leaveRequest);
                await _unitOfWork.Save();

                return RedirectToAction(nameof(MyLeave));

            }
            catch
            {
                ModelState.AddModelError("", "Something went wrong");
                return View(model);
            }
        }

        public async Task<ActionResult> CancelRequest(int id)
        {
            var leaveRequest = await _unitOfWork.LeaveRequests.Find(q=>q.Id == id);
            var employee = await _userManager.GetUserAsync(User);
            var leaveAllocation = await _unitOfWork.LeaveAllocations.Find(q => q.EmployeeId == employee.Id
                                                                               && q.Period == DateTime.Now.Year
                                                                               && q.LeaveTypeId == leaveRequest.LeaveTypeId);

            if (leaveRequest.Approved == true)
            {
                var daysRequested = (int)(leaveRequest.EndDate - leaveRequest.StartDate).TotalDays;
                leaveAllocation.NumberOfDays += daysRequested;
                _unitOfWork.LeaveAllocations.Update(leaveAllocation);
            }

            leaveRequest.Cancelled = true;
            _unitOfWork.LeaveRequests.Update(leaveRequest);
            await _unitOfWork.Save();

            return RedirectToAction(nameof(MyLeave));
        }

        // GET: LeaveRequestController/Edit/5
        public ActionResult Edit(int id)
        {
            return View();
        }

        // POST: LeaveRequestController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        // GET: LeaveRequestController/Delete/5
        public async Task<ActionResult> Delete(int id)
        {
            try
            {
                var leaveRequest = await _unitOfWork.LeaveRequests.Find(q => q.Id == id);
                var employee = await _userManager.GetUserAsync(User);
                var leaveAllocation = await _unitOfWork.LeaveAllocations.Find(q => q.EmployeeId == employee.Id
                    && q.Period == DateTime.Now.Year
                    && q.LeaveTypeId == leaveRequest.LeaveTypeId);

                if (leaveRequest.Approved == true)
                {
                    var daysRequested = (int)(leaveRequest.EndDate - leaveRequest.StartDate).TotalDays;
                    leaveAllocation.NumberOfDays += daysRequested;
                    _unitOfWork.LeaveAllocations.Update(leaveAllocation);
                }

                _unitOfWork.LeaveRequests.Delete(leaveRequest);
                
                return RedirectToAction(nameof(MyLeave));
            }
            catch
            {
                return RedirectToAction(nameof(MyLeave));
            }
        }

        // POST: LeaveRequestController/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(int id, LeaveRequestVM model)
        {
            try
            {
                var leaveRequest = await _unitOfWork.LeaveRequests.Find(q => q.Id == id);
                var employee = await _userManager.GetUserAsync(User);
                var leaveAllocation = await _unitOfWork.LeaveAllocations.Find(q => q.EmployeeId == employee.Id
                    && q.Period == DateTime.Now.Year
                    && q.LeaveTypeId == leaveRequest.LeaveTypeId);

                if (leaveRequest.Approved == true)
                {
                    var daysRequested = (int)(leaveRequest.EndDate - leaveRequest.StartDate).TotalDays;
                    leaveAllocation.NumberOfDays += daysRequested;
                    _unitOfWork.LeaveAllocations.Update(leaveAllocation);
                }

                _unitOfWork.LeaveRequests.Delete(leaveRequest);

                return RedirectToAction(nameof(MyLeave));
            }
            catch
            {
                return RedirectToAction(nameof(MyLeave));
            }
        }
        protected override void Dispose(bool disposing)
        {
            _unitOfWork.Dispose();
            base.Dispose(disposing);
        }
    }
}
