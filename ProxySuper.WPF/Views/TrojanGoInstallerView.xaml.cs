﻿using Microsoft.Win32;
using MvvmCross.Platforms.Wpf.Presenters.Attributes;
using MvvmCross.Platforms.Wpf.Views;
using ProxySuper.Core.Models.Hosts;
using ProxySuper.Core.Services;
using ProxySuper.Core.ViewModels;
using Renci.SshNet;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;

namespace ProxySuper.WPF.Views
{
    /// <summary>
    /// TrojanGoInstallerView.xaml 的交互逻辑
    /// </summary>
    [MvxWindowPresentation(Identifier = nameof(TrojanGoInstallerView), Modal = false)]
    public partial class TrojanGoInstallerView : MvxWindow
    {
        public TrojanGoInstallerView()
        {
            InitializeComponent();
        }

        public new TrojanGoInstallerViewModel ViewModel
        {
            get
            {
                return (TrojanGoInstallerViewModel)base.ViewModel;
            }
        }

        public TrojanGoProject Project { get; set; }



        private SshClient _sshClient;
        private void OpenConnect()
        {

            WriteOutput("正在登陆服务器 ...");
            var conneInfo = CreateConnectionInfo(ViewModel.Host);
            conneInfo.Timeout = TimeSpan.FromSeconds(60);
            _sshClient = new SshClient(conneInfo);
            try
            {
                _sshClient.Connect();
            }
            catch (Exception ex)
            {
                WriteOutput("登陆失败！");
                WriteOutput(ex.Message);
                return;
            }
            WriteOutput("登陆服务器成功！");

            ViewModel.Connected = true;
            Project = new TrojanGoProject(_sshClient, ViewModel.Settings, WriteOutput);
        }

        private void WriteOutput(string outShell)
        {
            if (!outShell.EndsWith("\n"))
            {
                outShell += "\n";
            }
            ViewModel.CommandText += outShell;

            Dispatcher.Invoke(() =>
            {
                OutputTextBox.AppendText(outShell);
                OutputTextBox.ScrollToEnd();
            });
        }

        private ConnectionInfo CreateConnectionInfo(Host host)
        {
            AuthenticationMethod auth = null;

            if (host.SecretType == LoginSecretType.Password)
            {
                auth = new PasswordAuthenticationMethod(host.UserName, host.Password);
            }
            else if (host.SecretType == LoginSecretType.PrivateKey)
            {
                auth = new PrivateKeyAuthenticationMethod(host.UserName, new PrivateKeyFile(host.PrivateKeyPath));
            }

            if (host.Proxy.Type == LocalProxyType.None)
            {
                return new ConnectionInfo(host.Address, host.Port, host.UserName, auth);
            }
            else
            {
                return new ConnectionInfo(
                    host: host.Address,
                    port: host.Port,
                    username: host.UserName,
                    proxyType: (ProxyTypes)(int)host.Proxy.Type,
                    proxyHost: host.Proxy.Address,
                    proxyPort: host.Proxy.Port,
                    proxyUsername: host.Proxy.UserName,
                    proxyPassword: host.Proxy.Password,
                    authenticationMethods: auth);
            }

        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            base.Loaded += (sender, arg) =>
            {
                Task.Factory.StartNew(OpenConnect);
            };
            base.Closed += SaveInstallLog;
            base.Closed += Disconnect;
        }

        private void Disconnect(object sender, EventArgs e)
        {
            if (_sshClient != null)
            {
                _sshClient.Disconnect();
                _sshClient.Dispose();
            }
        }

        private void SaveInstallLog(object sender, EventArgs e)
        {
            if (!Directory.Exists("Logs"))
            {
                Directory.CreateDirectory("Logs");
            }

            var fileName = System.IO.Path.Combine("Logs", DateTime.Now.ToString("yyyy-MM-dd hh-mm") + ".trojan-go.txt");
            File.WriteAllText(fileName, ViewModel.CommandText);
        }

        private void OpenLink(object sender, RoutedEventArgs e)
        {
            Hyperlink link = sender as Hyperlink;
            Process.Start(new ProcessStartInfo(link.NavigateUri.AbsoluteUri));
        }

        private void Install(object sender, RoutedEventArgs e)
        {
            Task.Factory.StartNew(Project.Install);
        }


        private void Uninstall(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("您确认要卸载代理吗？", "提示", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                Task.Factory.StartNew(Project.Uninstall);
            }
        }

        private void InstallCert(object sender, RoutedEventArgs e)
        {
            Task.Factory.StartNew(Project.InstallCertToTrojanGo);
        }


        private void UploadWeb(object sender, RoutedEventArgs e)
        {
            var fileDialog = new OpenFileDialog();
            fileDialog.Filter = "压缩文件|*.zip";
            fileDialog.FileOk += DoUploadWeb;
            fileDialog.ShowDialog();
        }

        private void DoUploadWeb(object sender, CancelEventArgs e)
        {
            Task.Factory.StartNew(() =>
            {
                var file = sender as OpenFileDialog;
                using (var stream = file.OpenFile())
                {
                    Project.UploadWeb(stream);
                }
            });
        }

    }
}
