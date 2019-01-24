﻿using System;
using System.Net.Sockets;
using System.Windows.Forms;
using Rssdp;
using NAudio.Wave;
using Microsoft.Practices.Unity;
using ChromeCast.Desktop.AudioStreamer.Communication;
using ChromeCast.Desktop.AudioStreamer.Communication.Classes;
using ChromeCast.Desktop.AudioStreamer.UserControls;
using ChromeCast.Desktop.AudioStreamer.Streaming.Interfaces;
using ChromeCast.Desktop.AudioStreamer.Communication.Interfaces;
using ChromeCast.Desktop.AudioStreamer.Classes;
using ChromeCast.Desktop.AudioStreamer.Streaming;
using ChromeCast.Desktop.AudioStreamer.ProtocolBuffer;

namespace ChromeCast.Desktop.AudioStreamer.Application
{
    public class Device : IDevice
    {
        private IDeviceCommunication deviceCommunication;
        private IStreamingConnection streamingConnection;
        private IDeviceConnection deviceConnection;
        private DiscoveredSsdpDevice discoveredSsdpDevice;
        private SsdpDevice ssdpDevice;
        private DeviceState deviceState;
        private DeviceControl deviceControl;
        private MenuItem menuItem;
        private Volume volumeSetting;
        private DateTime lastVolumeChange;

        public Device(IDeviceConnection deviceConnectionIn, IDeviceCommunication deviceCommunicationIn)
        {
            deviceConnection = deviceConnectionIn;
            deviceConnection.SetCallback(GetHost, SetDeviceState, OnReceiveMessage);
            deviceCommunication = deviceCommunicationIn;
            deviceCommunication.SetCallback(SetDeviceState, OnVolumeUpdate, deviceConnection.SendMessage, GetDeviceState, IsConnected, deviceConnection.IsConnected, GetHost);
            deviceState = DeviceState.NotConnected;
            volumeSetting = new Volume
            {
                controlType = "attenuation",
                level = 0.0f,
                muted = false,
                stepInterval = 0.05f
            };
        }

        public void SetDiscoveredDevices(DiscoveredSsdpDevice discoveredSsdpDeviceIn, SsdpDevice ssdpDeviceIn, ushort portIn)
        {
            discoveredSsdpDevice = discoveredSsdpDeviceIn;
            ssdpDevice = ssdpDeviceIn;
            deviceConnection.SetPort(portIn);
        }

        public void OnClickPlayPause()
        {
            deviceCommunication.OnPlayPause_Click();
        }

        private void OnClickStop()
        {
            deviceCommunication.OnStop_Click();
        }

        public void OnClickDeviceButton(object sender, EventArgs e)
        {
            deviceCommunication.OnPlayPause_Click();
        }

        public void Start()
        {
            deviceCommunication.LoadMedia();
        }

        public void OnRecordingDataAvailable(byte[] dataToSend, WaveFormat format, int reduceLagThreshold, SupportedStreamFormat streamFormat)
        {
            if (streamingConnection != null)
            {
                if (streamingConnection.IsConnected())
                {
                    if (deviceState != DeviceState.NotConnected)
                    {
                        streamingConnection.SendData(dataToSend, format, reduceLagThreshold, streamFormat);
                        if (deviceState != DeviceState.Buffering &&
                            deviceState != DeviceState.Playing &&
                            deviceState != DeviceState.Paused &&
                            deviceState != DeviceState.Idle)
                            SetDeviceState(DeviceState.Playing, "");
                    }
                }
                else
                {
                    Console.WriteLine(string.Format("Connection closed from {0}", streamingConnection.GetRemoteEndPoint()));
                    streamingConnection = null;
                }
            }
        }

        public void OnGetStatus()
        {
            deviceCommunication.GetStatus();
        }

        public void SetDeviceState(DeviceState state, string text = null)
        {
            deviceState = state;
            deviceControl?.SetStatus(state, text);
        }

        public bool IsConnected()
        {
            return !(deviceState.Equals(DeviceState.NotConnected) ||
                deviceState.Equals(DeviceState.ConnectError) ||
                deviceState.Equals(DeviceState.Closed));
        }

        public void OnVolumeUpdate(Volume volume)
        {
            volumeSetting = volume;
            deviceControl?.OnVolumeUpdate(volume);
        }

        public void VolumeSet(float level)
        {
            if (lastVolumeChange != null && DateTime.Now.Ticks - lastVolumeChange.Ticks < 1000)
                return;

            lastVolumeChange = DateTime.Now;

            if (volumeSetting.level > level)
                while (volumeSetting.level > level) volumeSetting.level -= volumeSetting.stepInterval;
            if (volumeSetting.level < level)
                while (volumeSetting.level < level) volumeSetting.level += volumeSetting.stepInterval;
            if (level > 1) level = 1;
            if (level < 0) level = 0;

            volumeSetting.level = level;
            deviceCommunication.VolumeSet(volumeSetting);
        }

        public void VolumeUp()
        {
            VolumeSet(volumeSetting.level + 0.05f);
        }

        public void VolumeDown()
        {
            VolumeSet(volumeSetting.level - 0.05f);
        }

        public void VolumeMute()
        {
            deviceCommunication.VolumeMute(!volumeSetting.muted);
        }

        public void Stop()
        {
            deviceCommunication.Stop();
            SetDeviceState(DeviceState.Closed);
        }

        public bool AddStreamingConnection(string remoteAddress, Socket socket)
        {
            if ((deviceState == DeviceState.LoadingMedia || 
                    deviceState == DeviceState.Buffering || 
                    deviceState == DeviceState.Idle) &&
                discoveredSsdpDevice.DescriptionLocation.Host.Equals(remoteAddress))
            {
                streamingConnection = DependencyFactory.Container.Resolve<StreamingConnection>();
                streamingConnection.SetSocket(socket);
                streamingConnection.SendStartStreamingResponse();
                return true;
            }

            return false;
        }

        public string GetUsn()
        {
            if (discoveredSsdpDevice != null)
                return discoveredSsdpDevice.Usn;

            return string.Empty;
        }

        public string GetHost()
        {
            if (discoveredSsdpDevice != null)
                return discoveredSsdpDevice.DescriptionLocation.Host;

            return string.Empty;
        }

        public string GetFriendlyName()
        {
            if (ssdpDevice != null)
                return ssdpDevice.FriendlyName;

            return string.Empty;
        }

        public DeviceState GetDeviceState()
        {
            return deviceState;
        }

        public DeviceControl GetDeviceControl()
        {
            return deviceControl;
        }

        public void SetDeviceControl(DeviceControl deviceControlIn)
        {
            deviceControl = deviceControlIn;
            deviceControl.SetClickCallBack(OnClickPlayPause, OnClickStop);
        }

        public void SetMenuItem(MenuItem menuItemIn)
        {
            menuItem = menuItemIn;
        }

        public MenuItem GetMenuItem()
        {
            return menuItem;
        }

        public IDeviceConnection GetDeviceConnection()
        {
            return deviceConnection;
        }

        public void OnReceiveMessage(CastMessage castMessage)
        {
            deviceCommunication?.OnReceiveMessage(castMessage);
        }

        public void SetDeviceName(string name)
        {
            deviceControl?.SetDeviceName(name);
            menuItem.Text = name;
        }

        public ushort GetPort()
        {
            return deviceConnection.GetPort();
        }
    }
}
