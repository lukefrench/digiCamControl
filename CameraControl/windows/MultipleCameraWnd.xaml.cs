﻿#region Licence

// Distributed under MIT License
// ===========================================================
// 
// digiCamControl - DSLR camera remote control open source software
// Copyright (C) 2014 Duka Istvan
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, 
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF 
// MERCHANTABILITY,FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. 
// IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY 
// CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
// TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH 
// THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

#endregion

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using CameraControl.Classes;
using CameraControl.Core;
using CameraControl.Core.Classes;
using CameraControl.Core.Interfaces;
using CameraControl.Devices;
using CameraControl.Devices.Classes;

#endregion

namespace CameraControl.windows
{
    /// <summary>
    /// Interaction logic for MultipleCameraWnd.xaml
    /// </summary>
    public partial class MultipleCameraWnd : IWindow
    {
        public bool DisbleAutofocus { get; set; }
        public int DelaySec { get; set; }
        public int WaitSec { get; set; }
        public int NumOfPhotos { get; set; }

        private System.Timers.Timer _timer = new System.Timers.Timer(1000);
        private int _secounter = 0;
        private int _photocounter = 0;

        public bool UseExternal { get; set; }

        public CustomConfig SelectedConfig { get; set; }

        public MultipleCameraWnd()
        {
            NumOfPhotos = 1;

            InitializeComponent();
            _timer.Elapsed += new ElapsedEventHandler(_timer_Elapsed);
        }

        private void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _secounter++;
            if (_secounter > WaitSec)
            {
                _secounter = 0;
                _timer.Stop();
                CapturePhotos();
            }
            else
            {
                StaticHelper.Instance.SystemMessage = string.Format("Waiting {0})", _secounter);
            }
        }

        #region Implementation of IWindow

        public void ExecuteCommand(string cmd, object param)
        {
            switch (cmd)
            {
                case WindowsCmdConsts.MultipleCameraWnd_Show:
                    Dispatcher.Invoke(new Action(delegate
                                                     {
                                                         Show();
                                                         Activate();
                                                         //Topmost = true;
                                                         //Topmost = false;
                                                         Focus();
                                                     }));
                    break;
                case WindowsCmdConsts.MultipleCameraWnd_Hide:
                    Hide();
                    break;
                case CmdConsts.All_Close:
                    Dispatcher.Invoke(new Action(delegate
                                                     {
                                                         Hide();
                                                         Close();
                                                     }));
                    break;
            }
        }

        #endregion

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (IsVisible)
            {
                e.Cancel = true;
                ServiceProvider.WindowsManager.ExecuteCommand(WindowsCmdConsts.MultipleCameraWnd_Hide);
            }
        }

        private void btn_shot_Click(object sender, RoutedEventArgs e)
        {
            if (UseExternal)
            {
                try
                {
                    if (SelectedConfig != null)
                        ServiceProvider.ExternalDeviceManager.AssertFocus(SelectedConfig);
                }
                catch (Exception exception)
                {
                    Log.Error("Error set focus", exception);
                    StaticHelper.Instance.SystemMessage = "Error set focus" + exception.Message;
                }
            }
            _secounter = 0;
            _photocounter = 0;
            _timer.Start();
        }

        private void CapturePhotos()
        {
            _photocounter++;
            StaticHelper.Instance.SystemMessage = string.Format("Capture started multiple cameras {0}", _photocounter);
            Thread thread = new Thread(new ThreadStart(delegate
                                                           {
                                                               while (CamerasAreBusy())
                                                               {
                                                               }
                                                               try
                                                               {
                                                                   if (UseExternal)
                                                                   {
                                                                       if (SelectedConfig != null)
                                                                       {
                                                                           ServiceProvider.ExternalDeviceManager.
                                                                               OpenShutter(SelectedConfig);
                                                                           Thread.Sleep(300);
                                                                           ServiceProvider.ExternalDeviceManager.
                                                                               CloseShutter(SelectedConfig);
                                                                       }
                                                                   }
                                                                   else
                                                                   {
                                                                       CameraHelper.CaptureAll(DelaySec);
                                                                   }
                                                               }
                                                               catch (Exception exception)
                                                               {
                                                                   Log.Error(exception);
                                                               }

                                                               Thread.Sleep(DelaySec);
                                                               if (_photocounter < NumOfPhotos)
                                                                   _timer.Start();
                                                               else
                                                               {
                                                                   StopCapture();
                                                               }
                                                           }));
            thread.Start();
        }


        private bool CamerasAreBusy()
        {
            return ServiceProvider.DeviceManager.ConnectedDevices.Aggregate(false,
                                                                            (current, connectedDevice) =>
                                                                            connectedDevice.IsBusy || current);
        }

        private void btn_stop_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            _photocounter = NumOfPhotos;
            StopCapture();
        }

        private void StopCapture()
        {
            if (UseExternal && SelectedConfig != null)
            {
                ServiceProvider.ExternalDeviceManager.CloseShutter(SelectedConfig);
                ServiceProvider.ExternalDeviceManager.DeassertFocus(SelectedConfig);
            }
            StaticHelper.Instance.SystemMessage = "All captures done !";
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (listBox1.SelectedItem != null)
                ServiceProvider.WindowsManager.ExecuteCommand(WindowsCmdConsts.CameraPropertyWnd_Show,
                                                              listBox1.SelectedItem);
        }

        private void MenuItem_Click_1(object sender, RoutedEventArgs e)
        {
            if (listBox1.SelectedItem != null)
            {
                CameraPreset preset = new CameraPreset();
                preset.Get((ICameraDevice) listBox1.SelectedItem);
                foreach (ICameraDevice connectedDevice in ServiceProvider.DeviceManager.ConnectedDevices)
                {
                    if (connectedDevice.IsConnected && connectedDevice.IsChecked)
                        preset.Set(connectedDevice);
                }
            }
        }

        private void btn_help_Click(object sender, RoutedEventArgs e)
        {
            HelpProvider.Run(HelpSections.MultipleCamera);
        }

        private void btn_resetCounters_Click(object sender, RoutedEventArgs e)
        {
            foreach (ICameraDevice connectedDevice in ServiceProvider.DeviceManager.ConnectedDevices)
            {
                CameraProperty property = ServiceProvider.Settings.CameraProperties.Get(connectedDevice);
                property.Counter = 0;
            }
        }

        private void btn_set_counter_Click(object sender, RoutedEventArgs e)
        {
            int counter = 0;
            if (int.TryParse(txt_counter.Text, out counter))
            {
                foreach (ICameraDevice connectedDevice in ServiceProvider.DeviceManager.ConnectedDevices)
                {
                    CameraProperty property = ServiceProvider.Settings.CameraProperties.Get(connectedDevice);
                    property.Counter = counter;
                }
            }
        }

        private void btn_focus_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedConfig != null)
                ServiceProvider.ExternalDeviceManager.Focus(SelectedConfig);
        }

        private void btn_capture_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedConfig != null)
                ServiceProvider.ExternalDeviceManager.Capture(SelectedConfig);
        }

        private void btn_stay_on_top_Click(object sender, RoutedEventArgs e)
        {
            Topmost = (btn_stay_on_top.IsChecked == true);
        }
    }
}