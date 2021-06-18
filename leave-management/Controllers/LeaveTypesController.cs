using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using leave_management.Contracts;
using leave_management.Data;
using leave_management.Models;
using Microsoft.AspNetCore.Authorization;

namespace leave_management.Controllers
{
    [Authorize (Roles = "Administrator")]
    public class LeaveTypesController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        
        public LeaveTypesController(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }
        
        // GET: LeaveTypesController
        public async Task<ActionResult> Index()
        {
            var leaveTypes = await _unitOfWork.LeaveTypes.FindAll();
            var model = _mapper.Map<List<LeaveType>, List<LeaveTypeVM>>(leaveTypes.ToList());
            return View(model);
        }

        // GET: LeaveTypesController/Details/5
        public async Task<ActionResult> Details(int id)
        {
            var isExists = await _unitOfWork.LeaveTypes.IsExists(q=>q.Id == id);
            if (!isExists)
            {
                return NotFound();
            }
            
            var leaveType = await _unitOfWork.LeaveTypes.Find(q => q.Id == id);
            var model = _mapper.Map<LeaveTypeVM>(leaveType);

            return View(model);
        }

        // GET: LeaveTypesController/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: LeaveTypesController/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(LeaveTypeVM model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return View(model);
                }

                var leaveType = _mapper.Map<LeaveType>(model);
                leaveType.DateCreated = DateTime.Now;
                
                await _unitOfWork.LeaveTypes.Create(leaveType);
                await _unitOfWork.Save();

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                ModelState.AddModelError("", "Something Went Wrong...");
                return View(model);
            }
        }

        // GET: LeaveTypesController/Edit/5
        public async Task<ActionResult> Edit(int id)
        {
            var isExists = await _unitOfWork.LeaveTypes.IsExists(q => q.Id == id);
            if (!isExists)
            {
                return NotFound();
            }
            
            var leaveType = await _unitOfWork.LeaveTypes.Find(q => q.Id == id);
            var model = _mapper.Map<LeaveTypeVM>(leaveType);

            return View(model);
        }

        // POST: LeaveTypesController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(LeaveTypeVM model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return View(model);
                }

                var leaveType = _mapper.Map<LeaveType>(model);
                
                _unitOfWork.LeaveTypes.Update(leaveType);
                await _unitOfWork.Save();

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                ModelState.AddModelError("", "Something Went Wrong...");
                return View(model);
            }
        }

        // POST: LeaveTypesController/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(int id)
        {
            try
            {
                var leaveType = await _unitOfWork.LeaveTypes.Find(q => q.Id == id);
                if (leaveType == null)
                {
                    return NotFound();
                }
                
                _unitOfWork.LeaveTypes.Delete(leaveType);
                await _unitOfWork.Save();

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return RedirectToAction(nameof(Index));
            }
        }
        protected override void Dispose(bool disposing)
        {
            _unitOfWork.Dispose();
            base.Dispose(disposing);
        }
    }
}
