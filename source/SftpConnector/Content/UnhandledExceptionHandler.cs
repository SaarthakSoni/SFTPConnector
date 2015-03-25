//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace SFTPConnector.Exceptions
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Web.Http.ExceptionHandling;

    /// <summary>
    /// logging unhandled exception 
    /// </summary>
    public class UnhandledExceptionHandler : ExceptionLogger
    {
        /// <summary>
        /// Overriden method from exceptionLogger to log the error
        /// </summary>
        /// <param name="context"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async override Task LogAsync(ExceptionLoggerContext context, System.Threading.CancellationToken cancellationToken)
        {
            Trace.TraceError(context.Exception.StackTrace);
            await base.LogAsync(context, cancellationToken);
        }
    }
}
