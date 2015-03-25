//-----------------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------

namespace SFTPConnector
{
    using System.Diagnostics.CodeAnalysis;
    using System.Net.Http.Formatting;
    using System.Web.Http;

    /// <summary>
    /// Web Api Config
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Api", Justification = "By design")]
    public static class WebApiConfig
    {
        /// <summary>
        /// Register
        /// </summary>
        /// <param name="config"></param>
        [SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "0", Justification = "By design")]
        public static void Register(HttpConfiguration config)
        {
            GlobalConfiguration.Configuration.Formatters.Clear();
            GlobalConfiguration.Configuration.Formatters.Add(new JsonMediaTypeFormatter());

            config.MapHttpAttributeRoutes(new SftpConnector.App_Start.CustomDirectRouteProvider());
            config.Routes.MapHttpRoute(
               name: "DefaultApi",
               routeTemplate: "{controller}/{action}");
        }
    }
}
