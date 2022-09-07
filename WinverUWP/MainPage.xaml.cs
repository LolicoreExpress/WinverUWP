﻿using RegistryRT;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.Resources;
using Windows.Foundation.Metadata;
using Windows.Storage;
using Windows.System.Profile;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace WinverUWP
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private Registry registry = new Registry();
        private string OSName = "";
        private ResourceLoader resourceLoader;
        private UISettings _uiSettings;
        private bool isCopying;

        public MainPage()
        {
            InitializeComponent();
                
            _uiSettings = new UISettings();

            resourceLoader = ResourceLoader.GetForCurrentView();

            var appdata = ApplicationData.Current.LocalSettings.Values.Where(f => f.Key.EndsWith("Expander"));

            if (appdata.Count() == 0)
                SpecExpander.IsExpanded = LegalExpander.IsExpanded = true;
            else
                foreach(var value in appdata)
                    ((Microsoft.UI.Xaml.Controls.Expander)FindName(value.Key)).IsExpanded = (bool)value.Value;

            ApplicationViewTitleBar titleBar = ApplicationView.GetForCurrentView().TitleBar;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = true;

            UpdateTitleBarLayout(coreTitleBar);
            coreTitleBar.LayoutMetricsChanged += CoreTitleBar_LayoutMetricsChanged;
            coreTitleBar.IsVisibleChanged += CoreTitleBar_IsVisibleChanged;

            Window.Current.SetTitleBar(TitleBar);

            Window.Current.Activated += Current_Activated;
        }

        private void CoreTitleBar_IsVisibleChanged(CoreApplicationViewTitleBar sender, object args)
        {
            TitleBar.Visibility = sender.IsVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CoreTitleBar_LayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
        {
            UpdateTitleBarLayout(sender);
        }

        private void Current_Activated(object sender, Windows.UI.Core.WindowActivatedEventArgs e)
        {
            if (e.WindowActivationState == Windows.UI.Core.CoreWindowActivationState.Deactivated)
                AppTitle.Style = (Style)Application.Current.Resources["InactivatedAppTitle"];
            else
                AppTitle.Style = (Style)Application.Current.Resources["ActivatedAppTitle"];
        }

        private void UpdateTitleBarLayout(CoreApplicationViewTitleBar coreTitleBar)
        {
            // Update title bar control size as needed to account for system size changes.
            TitleBar.Height = coreTitleBar.Height;

            // Ensure the custom title bar does not overlap window caption controls
            Thickness currMargin = TitleBar.Margin;
            TitleBar.Margin = new Thickness(currMargin.Left, currMargin.Top, coreTitleBar.SystemOverlayRightInset, currMargin.Bottom);
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (isCopying)
                return;

            isCopying = true;
            DataPackage package = new DataPackage();

            Dictionary<string, string> data = new Dictionary<string, string>()
            {
                { resourceLoader.GetString("Edition/Text"), Edition.Text },
                { resourceLoader.GetString("Version/Text"), Version.Text },
                { resourceLoader.GetString("InstalledOn/Text"), InstalledOn.Text },
                { resourceLoader.GetString("OSBuild/Text"), Build.Text },
            };

            if (Expiration.Visibility == Visibility.Visible)
                data.Add(resourceLoader.GetString("Expiration/Text"), Expiration.Text);

            int maxLength = data.Keys.Max(f => f.Length + 5);

            var lines = data.Select(f => string.Format($"{{0,-{maxLength}}}", f.Key) + f.Value);

            string targetText = string.Join(Environment.NewLine, lines);

            package.SetText(targetText);
            Clipboard.SetContent(package);
            CopyToClipboardSuccessAnimation.Begin();
            CopyToClipboardSuccessAnimation.Completed += CopyToClipboardSuccessAnimation_Completed;
        }

        private void CopyToClipboardSuccessAnimation_Completed(object sender, object e)
        {
            isCopying = false;
            CopyToClipboardSuccessAnimation.Completed -= CopyToClipboardSuccessAnimation_Completed;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // Interop.RoGetActivationFactory("Windows.Internal.StateRepository.Package", typeof(IPackageStatics_StateRepository).GUID, out object instance);
            // bool test = ((IPackageStatics_StateRepository)instance).ExistsByPackageFamilyName("47827AhmedWalid.FlairMaxBeta_hhm185gzkv8e8");

            string deviceFamilyVersion = AnalyticsInfo.VersionInfo.DeviceFamilyVersion;
            ulong version = ulong.Parse(deviceFamilyVersion);
            ulong build = (version & 0x00000000FFFF0000L) >> 16;
            var revision = version & 0x000000000000FFFF;

            OSName = build >= 21996 ? "Windows11Logo" : "Windows10Logo";
            UpdateWindowsBrand();

            _uiSettings.ColorValuesChanged += async (a, b) =>
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => UpdateWindowsBrand());
            };

            Build.Text = build.ToString();

            if (revision != 0)
                Build.Text += $".{revision}";

            registry.InitNTDLLEntryPoints();
            string productName = "";

            if (AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Desktop")
                productName = WinverNative.Winbrand.BrandingFormatString("%WINDOWS_LONG%");
            else
                productName = ReturnValueFromRegistry(RegistryHive.HKEY_LOCAL_MACHINE, "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion", "ProductName");

            Edition.Text = productName;
            LicensingText.Text = resourceLoader.GetString("Trademark/Text").Replace("Windows", productName);

            var displayVersion = ReturnValueFromRegistry(RegistryHive.HKEY_LOCAL_MACHINE, "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion", "DisplayVersion");
            if (string.IsNullOrEmpty(displayVersion))
                displayVersion = ReturnValueFromRegistry(RegistryHive.HKEY_LOCAL_MACHINE, "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion", "ReleaseId");
            Version.Text = displayVersion;

            var date = GetWindowsInstallationDateTime().ToLocalTime();
            var userCulture = CultureInfoHelper.GetCurrentCulture();
            InstalledOn.Text = date.ToString("d", userCulture);

            using (X509Certificate2 cert = new X509Certificate2("C:\\Windows\\System32\\ntdll.dll"))
            {
                if (cert.Issuer.Contains("Development"))
                    Expiration.Text = cert.NotAfter.ToString("g", userCulture);
                else
                {
                    Expiration.Visibility = Visibility.Collapsed;
                    ExpirationLabel.Visibility = Visibility.Collapsed;
                }
            }

            var ownerName = ReturnValueFromRegistry(RegistryHive.HKEY_LOCAL_MACHINE, "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion", "RegisteredOwner");
            OwnerText.Text = ownerName;
            OwnerText.Visibility = string.IsNullOrEmpty(ownerName) ? Visibility.Collapsed : Visibility.Visible;

            var ownerOrg = ReturnValueFromRegistry(RegistryHive.HKEY_LOCAL_MACHINE, "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion", "RegisteredOrganization");
            OrgText.Text = ownerOrg;
            OrgText.Visibility = string.IsNullOrEmpty(ownerOrg) ? Visibility.Collapsed : Visibility.Visible;

            if (string.IsNullOrEmpty(ownerName) && string.IsNullOrEmpty(ownerOrg))
                LicenseTo.Visibility = Visibility.Collapsed;
        }

        private void UpdateWindowsBrand()
        {
            BrandImage.Source = new SvgImageSource(
                new Uri(
                    "ms-appx:///Assets/"
                    + OSName
                    + "-"
                    + (Application.Current.RequestedTheme == ApplicationTheme.Dark ? "light" : "dark")
                    + ".svg"));
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            _ = ApplicationView.GetForCurrentView().TryConsolidateAsync();
        }

        private string ReturnValueFromRegistry(RegistryHive hive, string key, string value)
        {
            var success = registry.QueryValue(hive, key, value, out RegistryType _, out byte[] rawData);
            if (!success)
                return "";
            return Encoding.Unicode.GetString(rawData).Replace("\0", "");
        }

        private DateTime GetWindowsInstallationDateTime()
        {
            bool success = registry.QueryValue(RegistryHive.HKEY_LOCAL_MACHINE, "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion", "InstallDate", out RegistryType _, out byte[] buffer);
            if (!success)
                throw new Exception();

            var seconds = BitConverter.ToInt32(buffer, 0);
            DateTime startDate = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime installDate = startDate.AddSeconds(seconds);
            return installDate;
        }

        private void Expander_Collapsed(Microsoft.UI.Xaml.Controls.Expander sender, Microsoft.UI.Xaml.Controls.ExpanderCollapsedEventArgs args)
            => ApplicationData.Current.LocalSettings.Values[sender.Name] = false;

        private void Expander_Expanding(Microsoft.UI.Xaml.Controls.Expander sender, Microsoft.UI.Xaml.Controls.ExpanderExpandingEventArgs args)
            => ApplicationData.Current.LocalSettings.Values[sender.Name] = true;
    }
}
