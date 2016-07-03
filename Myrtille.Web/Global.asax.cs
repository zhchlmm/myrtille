﻿/*
    Myrtille: A native HTML4/5 Remote Desktop Protocol client.

    Copyright(c) 2014-2016 Cedric Coste

    Licensed under the Apache License, Version 2.0 (the "License");
    you may not use this file except in compliance with the License.
    You may obtain a copy of the License at

        http://www.apache.org/licenses/LICENSE-2.0

    Unless required by applicable law or agreed to in writing, software
    distributed under the License is distributed on an "AS IS" BASIS,
    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
    See the License for the specific language governing permissions and
    limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Web;
using System.Web.Configuration;
using log4net.Config;
using Myrtille.Fleck;

namespace Myrtille.Web
{
    public enum HttpApplicationStateVariables
    {
        Cache,
        RemoteSessionsCounter,
        RemoteSessionsManagers,
        WebSocketServerPort,
        WebSocketServerPortSecured
    }

    public enum HttpSessionStateVariables
    {
        RemoteSessionManager
    }

    public class Global : HttpApplication
    {
        protected void Application_Start(
            object sender,
            EventArgs e)
        {
            try
            {
                // logger
                XmlConfigurator.Configure();

                // application cache
                Application[HttpApplicationStateVariables.Cache.ToString()] = Context.Cache;

                // remote sessions auto-incremented counter
                Application[HttpApplicationStateVariables.RemoteSessionsCounter.ToString()] = 0;

                // remote sessions managers
                Application[HttpApplicationStateVariables.RemoteSessionsManagers.ToString()] = new Dictionary<string, RemoteSessionManager>();

                // start a websocket server on a standard port; the port must be opened into your firewall (incoming traffic)
                var webSocketServerPort = 8181;
                if (!string.IsNullOrEmpty(WebConfigurationManager.AppSettings["WebSocketServerPort"]))
                {
                    webSocketServerPort = int.Parse(WebConfigurationManager.AppSettings["WebSocketServerPort"]);
                }
                Application[HttpApplicationStateVariables.WebSocketServerPort.ToString()] = webSocketServerPort;
                var server = new WebSocketServer(string.Format("ws://0.0.0.0:{0}", webSocketServerPort));
                new RemoteSessionSocketServer(this, server);

                // to use secure websockets, you must first export your IIS SSL certificate in .PFX format, with the private key and a password
                // save it into the application "ssl" folder under the name "PKCS12Cert.pfx" (or a name matching the Web.config key "WebSocketServerCertificateFilename")
                // make sure the file have the appropriate rights for IIS/ASP.NET to read it (NETWORK SERVICE and USER granted read access)
                // then update the Web.config key "WebSocketServerCertificatePassword" with your password (you may encrypt Web.config if you wish...)

                // certificate filename
                var webSocketServerCertificateFilename = "ssl\\PKCS12Cert.pfx";
                if (!string.IsNullOrEmpty(WebConfigurationManager.AppSettings["webSocketServerCertificateFilename"]))
                {
                    webSocketServerCertificateFilename = WebConfigurationManager.AppSettings["webSocketServerCertificateFilename"];
                }

                // check existence of a certificate (note that ".pfx" MIME type is blocked for download for the application...)
                if (!File.Exists(Path.Combine(Server.MapPath("~"), webSocketServerCertificateFilename)))
                {
                    Application[HttpApplicationStateVariables.WebSocketServerPortSecured.ToString()] = null;
                }
                else
                {
                    // start a websocket server on a secured port; the port must be opened into your firewall (incoming traffic)
                    var webSocketServerPortSecured = 8431;
                    if (!string.IsNullOrEmpty(WebConfigurationManager.AppSettings["WebSocketServerPortSecured"]))
                    {
                        webSocketServerPortSecured = int.Parse(WebConfigurationManager.AppSettings["WebSocketServerPortSecured"]);
                    }
                    Application[HttpApplicationStateVariables.WebSocketServerPortSecured.ToString()] = webSocketServerPortSecured;
                    server = new WebSocketServer(string.Format("wss://0.0.0.0:{0}", webSocketServerPortSecured));

                    // certificate password
                    var WebSocketServerCertificatePassword = "myrtille";
                    if (!string.IsNullOrEmpty(WebConfigurationManager.AppSettings["WebSocketServerCertificatePassword"]))
                    {
                        WebSocketServerCertificatePassword = WebConfigurationManager.AppSettings["WebSocketServerCertificatePassword"];
                    }

                    server.Certificate = new X509Certificate2(Path.Combine(Server.MapPath("~"), webSocketServerCertificateFilename), WebSocketServerCertificatePassword);
                    new RemoteSessionSocketServer(this, server);
                }
            }
            catch (Exception exc)
            {
                Trace.TraceError("Failed to start Myrtille.Web application ({0})", exc);
                throw;
            }
        }
    }
}