﻿/**
 * Passbolt ~ Open source password manager for teams
 * Copyright (c) Passbolt SA (https://www.passbolt.com)
 *
 * Licensed under GNU Affero General Public License version 3 of the or any later version.
 * For full copyright and license information, please see the LICENSE.txt
 * Redistributions of files must retain the above copyright notice.
 *
 * @copyright     Copyright (c) Passbolt SA (https://www.passbolt.com)
 * @license       https://opensource.org/licenses/AGPL-3.0 AGPL License
 * @link          https://www.passbolt.com Passbolt(tm)
 * @since         0.0.1
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using passbolt.Models;
using passbolt.Models.Messaging;
using passbolt.Services.NavigationService;
using passbolt.Utils;
using Windows.ApplicationModel;
using Windows.Storage;

namespace passbolt.Controllers
{
    public class MainController
    {
        private WebView2 webviewRendered;
        private WebView2 webviewBackground;
        private string blankPage = "about:blank";
        protected StorageFolder distfolder;
        protected RenderedTopic renderedTopic;
        protected BackgroundTopic backgroundTopic;
        protected RenderedNavigationService renderedNavigationService;
        protected BackgroundNavigationService backgroundNavigationService;

        public MainController(
            WebView2 webviewRendered, WebView2 webviewBackground) {
            this.webviewBackground = webviewBackground;
            this.webviewRendered = webviewRendered;
        }


        /// <summary>
        /// Navigation starting event handler for the background webview
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public async Task BackgroundNavigationStarting(WebView2 sender, CoreWebView2NavigationStartingEventArgs args)
        {
            if (webviewBackground == null) { return; }

            //This is the navigation guard of our webviews
            this.AllowNavigation(sender, args, this.backgroundNavigationService);

            //Webviews are loaded in the background, the default url is about:blank. In case this url is loaded, we load the webviews with the correct urls.
            if (args.Uri == this.blankPage)
            {
                await this.LoadWebviews();
                this.SetWebviewSettings(webviewBackground);
                webviewBackground.CoreWebView2.OpenDevToolsWindow();
            }
        }

        /// <summary>
        /// Navigation starting event handler for the rendered webview
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public void RenderedNavigationStarting(WebView2 sender, CoreWebView2NavigationStartingEventArgs args)
        {
            if (webviewRendered == null) { return; }

            //This is the navigation guard of our webviews
            this.AllowNavigation(sender, args, this.renderedNavigationService);

            //Webviews are loaded in the background, the default url is about:blank. In case this url is loaded, we load the webviews with the correct urls.
            if (args.Uri == this.blankPage)
            {
                this.SetWebviewSettings(webviewRendered);
            }
        }
        /// <summary>
        /// load the background web view
        /// </summary>
        public virtual async Task LoadWebviews()
        {
            await webviewRendered.EnsureCoreWebView2Async();
            await webviewBackground.EnsureCoreWebView2Async();
            
            string randomUrl = Guid.NewGuid().ToString();
            string backgroundUrl = randomUrl + "/Background";
            string renderedUrl = randomUrl + "/Rendered";

            this.backgroundNavigationService = new BackgroundNavigationService(backgroundUrl);
            this.renderedNavigationService = new RenderedNavigationService(renderedUrl);

            StorageFolder installationFolder = Package.Current.InstalledLocation;

            // Load dist folder to insert into the virtual host to avoid exception during testing
            if (distfolder == null)
                distfolder = await installationFolder.GetFolderAsync("Webviews");

            // Set virtual host to folder mapping, restrict host access to the randomUrl
            webviewBackground.CoreWebView2.SetVirtualHostNameToFolderMapping(randomUrl, distfolder.Path, CoreWebView2HostResourceAccessKind.DenyCors);
            webviewRendered.CoreWebView2.SetVirtualHostNameToFolderMapping(randomUrl, distfolder.Path, CoreWebView2HostResourceAccessKind.DenyCors);

            // Set the source for background webview
            webviewBackground.Source = new Uri(UriBuilderHelper.BuildHostUri(backgroundUrl, "index.html"));
            webviewRendered.Source = new Uri(UriBuilderHelper.BuildHostUri(renderedUrl, "index.html"));
            // Subscribes to the WebMessageReceived event of the rendered adn background window
            webviewBackground.CoreWebView2.WebMessageReceived += WebMessageReceived;
            webviewRendered.CoreWebView2.WebMessageReceived += WebMessageReceived;
        }

        /// <summary>
        /// Set the webview settings including minimal security requirements
        /// </summary>
        /// <param name="webView"></param>
        public virtual void SetWebviewSettings(WebView2 webView)
        {
            // Remove devtools from settings
            webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            // Remove swipe navigation
            webView.CoreWebView2.Settings.IsSwipeNavigationEnabled = false;
            // Remove autosaved password
            webView.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
            // New host cannot be added
            webView.CoreWebView2.Settings.AreHostObjectsAllowed = false;
            // Dialog will not be allowed
            webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
            // Remove contextual menu
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            // Remove accelerator keys like f12
            webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
            // Attach new events
            webView.CoreWebView2.NewWindowRequested += NewWindowRequested;
        }

        /// <summary>
        /// This method is called when webviews request a new windows to be opened.
        /// </summary>
        public virtual void NewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
        {
            args.Handled = true;
        }

        /// <summary>
        /// Initialised the background page and inject script
        /// </summary>
        /// <returns></returns>
        public virtual async Task WebviewInitialisation()
        {
            // Load script to inject for webviews
            StorageFolder backgroundFolder = await this.FindInWebviewFolder("Background");
            StorageFolder renderedFolder = await this.FindInWebviewFolder("Rendered");

            StorageFolder distBackgroundFolder = await backgroundFolder.GetFolderAsync("dist");
            StorageFolder distRenderedFolder = await renderedFolder.GetFolderAsync("dist");

            StorageFile backgroundFile = await distBackgroundFolder.GetFileAsync("background.js");
            StorageFile bundleFile = await distRenderedFolder.GetFileAsync("bundle.js");

            Stream streamBackgroundJS = await backgroundFile.OpenStreamForReadAsync();
            string scriptBackground = new StreamReader(streamBackgroundJS).ReadToEnd();
            await webviewBackground.ExecuteScriptAsync(scriptBackground);

            Stream streamRenderedJS = await bundleFile.OpenStreamForReadAsync();
            string scriptRendered = new StreamReader(streamRenderedJS).ReadToEnd();
            await webviewRendered.ExecuteScriptAsync(scriptRendered);
        }

        /// <summary>
        /// Check Navigation for webviews2
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public virtual void AllowNavigation(WebView2 sender, CoreWebView2NavigationStartingEventArgs args, AbstractNavigationService navigationService)
        {
            if (navigationService != null && !navigationService.canNavigate(args.Uri))
            {
                args.Cancel = true;
            }
        }

        /// <summary>
        /// Listener for webviews message received
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string message = e.TryGetWebMessageAsString();
            CoreWebView2 webviewSender = this.GetCoreWebView2Sender(sender);

            if (webviewSender == null || message == null) return;
            var ipc = SerializationHelper.DeserializeFromJson<IPC>(message);

            //Checks if we have data before going futher
            if (string.IsNullOrEmpty(ipc.topic) || !AllowedTopics.IsTopicNameAllowed(ipc.topic)) 
            {
                return;
            }

            // We identify the sender to proceed message by his source
            if (backgroundNavigationService.canNavigate(webviewSender.Source))
            {
                backgroundTopic.ProceedMessage(ipc);
            }
            else if (renderedNavigationService.canNavigate(webviewSender.Source))
            {
                renderedTopic.ProceedMessage(ipc);
            }
        }


        /// <summary>
        /// When background webview has finished his navigation
        /// </summary>
        /// <param name="sender"></param>
        /// <returns></returns>
        public async Task BackgroundNavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            Debug.WriteLine("NavigationCompleted: " + sender.CoreWebView2.Source);
            if (args.IsSuccess)
            {
                var message = new IPC(AllowedTopics.INITIALIZATION);
                await webviewBackground.EnsureCoreWebView2Async();
                await webviewRendered.EnsureCoreWebView2Async();
                this.renderedTopic = new RenderedTopic(webviewBackground, webviewRendered);
                this.backgroundTopic = new BackgroundTopic(webviewBackground, webviewRendered);
                webviewBackground.CoreWebView2.PostWebMessageAsJson(SerializationHelper.SerializeToJson(message));
                webviewRendered.CoreWebView2.PostWebMessageAsJson(SerializationHelper.SerializeToJson(message));
                await this.WebviewInitialisation();
            }
        }

        /// <summary>
        /// When rendered webview has finished his navigation
        /// </summary>
        /// <param name="sender"></param>
        /// <returns></returns>
        public async Task RenderedNavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            Debug.WriteLine("NavigationCompleted: " + sender.CoreWebView2.Source);
            if (args.IsSuccess)
            {
                await this.WebviewInitialisation();
            }
        }
        /// <summary>
        /// Retrieve the Corewebview from the sender
        /// </summary>
        /// <param name="sender"></param>
        /// <returns></returns>
        private CoreWebView2 GetCoreWebView2Sender(object sender)
        {
            CoreWebView2 webviewSender = null;
            try
            {
                if (sender is CoreWebView2)
                {
                    webviewSender = (CoreWebView2)sender;
                }
                else
                {
                    webviewSender = ((WebView2)sender).CoreWebView2;
                }
            }
            catch (InvalidCastException ex)
            {
                Console.WriteLine(ex.Message);
            }

            return webviewSender;
        }

        /// <summary>
        /// find folder into the webview folder
        /// </summary>
        /// <param name="webviewFolder"></param>
        /// <returns> The Storage folder found </returns>
        private async Task<StorageFolder> FindInWebviewFolder(string webviewFolder)
        {
            StorageFolder installationFolder = Package.Current.InstalledLocation;
            StorageFolder viewsFolder = await installationFolder.GetFolderAsync("Webviews");
            return await viewsFolder.GetFolderAsync(webviewFolder);
        }
    }
}
