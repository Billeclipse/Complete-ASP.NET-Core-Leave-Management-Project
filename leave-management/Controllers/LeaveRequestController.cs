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

namespace leave_management.Controllers
{
    [Authorize]
    public class LeaveRequestController : Controller
    {
        private readonly ILeaveRequestRepository _leaveRequestRepo;
        private readonly ILeaveTypeRepository _leaveTypeRepo;
        public readonly ILeaveAllocationRepository _leaveAllocationRepo;
        private readonly IMapper _mapper;
        private readonly UserManager<Employee> _userManager;

        public LeaveRequestController(
            ILeaveRequestRepository leaveRequestRepo,
            ILeaveTypeRepository leaveTypeRepo,
            ILeaveAllocationRepository leaveAllocationRepo,
            IMapper mapper,
            UserManager<Employee> userManager)
        {
            _leaveRequestRepo = leaveRequestRepo;
            _leaveTypeRepo = leaveTypeRepo;
            _leaveAllocationRepo = leaveAllocationRepo;
            _mapper = mapper;
            _userManager = userManager;
        }

        [Authorize(Roles = "Administrator")]
        // GET: LeaveRequestController
        public ActionResult Index()
        {
            var leaveRequests = _leaveRequestRepo.FindAll();
            var leaveRequestsModels = _mapper.Map<List<LeaveRequestVM>>(leaveRequests);
            var model = new AdminLeaveRequestViewVM()
            {
                TotalRequests = leaveRequestsModels.Count,
                ApprovedRequests = leaveRequestsModels.Count(q => q.Approved == true),
                PendingRequests = leaveRequestsModels.Count(q => q.Approved == null),
                RejectedRequests = leaveRequestsModels.Count(q => q.Approved == false),
                LeaveRequests = leaveRequestsModels
            };

            return View(model);
        }

        public ActionResult MyLeave()
        {
            var employee = _userManager.GetUserAsync(User).Result;
            var leaveRequests = _leaveRequestRepo.GetLeaveRequestsByEmployee(employee.Id);
            var leaveRequestsModels = _mapper.Map<List<LeaveRequestVM>>(leaveRequests);
            
            var leaveAllocations = _leaveAllocationRepo.GetLeaveAllocationsByEmployee(employee.Id);
            var leaveAllocationsModels = _mapper.Map<List<LeaveAllocationVM>>(leaveAllocations);
            
            var model = new EmployeeLeaveRequestViewVM()
            {
                LeaveRequests = leaveRequestsModels,
                LeaveAllocations = leaveAllocationsModels
            };

            return View(model);
        }

        // GET: LeaveRequestController/Details/5
        public ActionResult Details(int id)
        {
            var leaveRequest = _leaveRequestRepo.FindById(id);
            var model = _mapper.Map<LeaveRequestVM>(leaveRequest);
            return View(model);
        }

        public ActionResult ApproveRequest(int id)
        {
            try
            {
                var user = _userManager.GetUserAsync(User).Result;
                var leaveRequest = _leaveRequestRepo.FindById(id);
                var employeeId = leaveRequest.RequestingEmployeeId;
                var leaveTypeId = leaveRequest.LeaveTypeId;
                var allocation = _leaveAllocationRepo.GetLeaveAllocationsByEmployeeAndType(employeeId, leaveTypeId);
                var daysRequested = (int)(leaveRequest.EndDate - leaveRequest.StartDate).TotalDays;
                allocation.NumberOfDays -= daysRequested;

                leaveRequest.Approved = true;
                leaveRequest.ApprovedById = user.Id;
                leaveRequest.DateActioned = DateTime.Now;
                _leaveRequestRepo.Update(leaveRequest);
                _leaveAllocationRepo.Update(allocation);

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return RedirectToAction(nameof(Index));
            }
        }

        public ActionResult RejectRequest(int id)
        {
            try
            {
                var user = _userManager.GetUserAsync(User).Result;
                var leaveRequest = _leaveRequestRepo.FindById(id);
                leaveRequest.Approved = false;
                leaveRequest.ApprovedById = user.Id;
                leaveRequest.DateActioned = DateTime.Now;
                _leaveRequestRepo.Update(leaveRequest);

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: LeaveRequestController/Create
        public ActionResult Create()
        {
            var leaveTypes = _leaveTypeRepo.FindAll();
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
        public ActionResult Create(CreateLeaveRequestVM model)
        {
            try
            {
                var startDate = Convert.ToDateTime(model.StartDate);
                var endDate = Convert.ToDateTime(model.EndDate);
                var leaveTypes = _leaveTypeRepo.FindAll();
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

                var employee = _userManager.GetUserAsync(User).Result;
                var allocation = _leaveAllocationRepo.GetLeaveAllocationsByEmployeeAndType(employee.Id, model.LeaveTypeId);
                var daysRequested = (int)(endDate - startDate).TotalDays;

                if (daysRequested > allocation.NumberOfDays)
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
                var isSuccess = _leaveRequestRepo.Create(leaveRequest);

                if (!isSuccess)
                {
                    ModelState.AddModelError("", "Something went wrong with submitting your record");
                    return View(model);
                }

                return RedirectToAction(nameof(Index), "Home");
            }
            catch
            {
                ModelState.AddModelError("", "Something went wrong");
                return View(model);
            }
        }

        public ActionResult CancelRequest(int id)
        {
            var leaveRequest = _leaveRequestRepo.FindById(id);
            var employee = _userManager.GetUserAsync(User).Result;
            var leaveAllocation = _leaveAllocationRepo.GetLeaveAllocationsByEmployeeAndType(employee.Id, leaveRequest.LeaveTypeId);

            if (leaveRequest.Approved == true)
            {
                var daysRequested = (int)(leaveRequest.EndDate - leaveRequest.StartDate).TotalDays;
                leaveAllocation.NumberOfDays += daysRequested;
                _leaveAllocationRepo.Update(leaveAllocation);
            }

            leaveRequest.Cancelled = true;
            _leaveRequestRepo.Update(leaveRequest);
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
        public ActionResult Delete(int id)
        {
            try
            {
                var leaveRequest = _leaveRequestRepo.FindById(id);
                var employee = _userManager.GetUserAsync(User).Result;
                var leaveAllocation = _leaveAllocationRepo.GetLeaveAllocationsByEmployeeAndType(employee.Id, leaveRequest.LeaveTypeId);

                if (leaveRequest.Approved == true)
                {
                    var daysRequested = (int)(leaveRequest.EndDate - leaveRequest.StartDate).TotalDays;
                    leaveAllocation.NumberOfDays += daysRequested;
                    _leaveAllocationRepo.Update(leaveAllocation);
                }
                var isSuccess = _leaveRequestRepo.Delete(leaveRequest);
                if (!isSuccess)
                {
                    return BadRequest();
                }
                
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
        public ActionResult Delete(int id, LeaveRequestVM model)
        {
            try
            {
                var employee = _userManager.GetUserAsync(User).Result;
                var leaveAllocation = _leaveAllocationRepo.GetLeaveAllocationsByEmployeeAndType(employee.Id, model.LeaveTypeId);
                var leaveRequest = _leaveRequestRepo.FindById(id);
                if (leaveRequest == null)
                {
                    return NotFound();
                }

                if (leaveRequest.Approved == true)
                {
                    var daysRequested = (int)(leaveRequest.EndDate - leaveRequest.StartDate).TotalDays;
                    leaveAllocation.NumberOfDays -= daysRequested;
                    _leaveAllocationRepo.Update(leaveAllocation);
                }

                _leaveRequestRepo.Delete(leaveRequest);

                return RedirectToAction(nameof(MyLeave));
            }
            catch
            {
                return RedirectToAction(nameof(MyLeave));
            }
        }
    }
}
