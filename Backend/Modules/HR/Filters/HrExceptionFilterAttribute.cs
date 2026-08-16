using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MyApi.Modules.HR.Filters
{
    /// <summary>
    /// Maps domain exceptions thrown by <c>HrService</c> onto proper HTTP status codes.
    /// Without this, a delete/update against a missing row bubbles up as a 500, which the
    /// frontend cannot distinguish from a real server fault.
    /// </summary>
    public class HrExceptionFilterAttribute : ExceptionFilterAttribute
    {
        public override void OnException(ExceptionContext context)
        {
            switch (context.Exception)
            {
                case KeyNotFoundException knf:
                    context.Result = new NotFoundObjectResult(new { success = false, message = knf.Message });
                    context.ExceptionHandled = true;
                    break;
                case InvalidOperationException ioe:
                    // Business-rule violations (e.g. locked payroll run, duplicate period).
                    context.Result = new BadRequestObjectResult(new { success = false, message = ioe.Message });
                    context.ExceptionHandled = true;
                    break;
                case ArgumentException ae:
                    context.Result = new BadRequestObjectResult(new { success = false, message = ae.Message });
                    context.ExceptionHandled = true;
                    break;
            }
        }
    }
}
