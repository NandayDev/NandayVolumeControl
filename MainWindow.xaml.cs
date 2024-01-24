using CoreAudio;
using Microsoft.Win32;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NandayVolumeControl
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MMDeviceEnumerator deviceEnumerator = new(Guid.NewGuid());
        private readonly MMNotificationClient client;
        private Device? currentSpeaker, currentMicrophone;

        public MainWindow()
        {
            InitializeComponent();
            client = new(deviceEnumerator);
            InitializeDevices();
            KeyDown += ExitOnEscPressed;
            Closing += OnClosing;
            volumeSlider.ValueChanged += VolumeSlider_ValueChanged;
            microphoneSlider.ValueChanged += MicrophoneSlider_ValueChanged;
            SystemEvents.PowerModeChanged += delegate { InitializeDevices(); };
        }

        private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (MessageBox.Show("Uscire?", "Esci", MessageBoxButton.YesNo) == MessageBoxResult.No)
            {
                e.Cancel = true;
            }
        }

        private void ExitOnEscPressed(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        private void DragOnMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (e.ChangedButton == MouseButton.Left)
                    DragMove();
            }
            catch { }
        }

        private void InitializeDevices(Device? newSpeakerDevice = null, Device? newMicrophoneDevice = null)
        {
            Dispatcher.Invoke(delegate
            {
                speakersComboBox.SelectionChanged -= OnComboBoxSelectionChanged;
                microphoneComboBox.SelectionChanged -= OnComboBoxSelectionChanged;
                RemoveDeviceCallbacks();
                currentSpeaker = GetCurrentDevice(DataFlow.Render, speakersComboBox, newSpeakerDevice);
                currentMicrophone = GetCurrentDevice(DataFlow.Capture, microphoneComboBox, newMicrophoneDevice);
                if (currentSpeaker == null || currentMicrophone == null)
                {
                    throw new Exception("Speaker or microphone null");
                }
                InitializeSlider(currentSpeaker, volumeSlider);
                InitializeSlider(currentMicrophone, microphoneSlider);
                AddDeviceCallbacks();
                speakersComboBox.SelectionChanged += OnComboBoxSelectionChanged;
                microphoneComboBox.SelectionChanged += OnComboBoxSelectionChanged;
            });
        }

        private Device? GetCurrentDevice(DataFlow dataFlow, ComboBox comboBox, Device? newDefaultDevice)
        {
            var nAudioDevices = deviceEnumerator.EnumerateAudioEndPoints(dataFlow, DeviceState.Active).ToDevices();
            comboBox.ItemsSource = nAudioDevices;
            var defaultDevice = newDefaultDevice ?? deviceEnumerator.GetDefaultAudioEndpoint(dataFlow, Role.Console).ToDevice();
            comboBox.SelectedIndex = nAudioDevices.IndexOf(nAudioDevices.First(d => d.Id == defaultDevice.Id));
            if (dataFlow == DataFlow.Render)
            {
                return comboBox.SelectedItem as Device;
            }
            else
            {
                return comboBox.SelectedItem as Device;
            }
        }

        private void OnComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            deviceEnumerator.SetDefaultAudioEndpoint((e.AddedItems[0] as Device)!.CoreAudioDevice);
        }

        private void InitializeSlider(Device device, Slider slider)
        {
            if (device?.CoreAudioDevice?.AudioEndpointVolume is AudioEndpointVolume volume)
            {
                slider.Value = volume.MasterVolumeLevelScalar;
                if (device.CoreAudioDevice.DataFlow == DataFlow.Render)
                {
                    volume.OnVolumeNotification -= Speaker_OnVolumeNotification;
                    volume.OnVolumeNotification += Speaker_OnVolumeNotification;
                }
                else
                {
                    volume.OnVolumeNotification -= Microphone_OnVolumeNotification;
                    volume.OnVolumeNotification += Microphone_OnVolumeNotification;
                }
            }
        }

        private void Speaker_OnVolumeNotification(AudioVolumeNotificationData data)
        {
            Dispatcher.Invoke(delegate
            {
                volumeSlider.ValueChanged -= VolumeSlider_ValueChanged;
                volumeSlider.Value = data.MasterVolume;
                volumeSlider.ValueChanged += VolumeSlider_ValueChanged;
            });
        }

        private void Microphone_OnVolumeNotification(AudioVolumeNotificationData data)
        {
            Dispatcher.Invoke(delegate
            {
                microphoneSlider.ValueChanged -= MicrophoneSlider_ValueChanged;
                microphoneSlider.Value = data.MasterVolume;
                microphoneSlider.ValueChanged += MicrophoneSlider_ValueChanged;
            });
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (currentSpeaker?.CoreAudioDevice?.AudioEndpointVolume is AudioEndpointVolume volume)
            {
                volume.MasterVolumeLevelScalar = (float)e.NewValue;
            }
        }

        private void MicrophoneSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (currentMicrophone?.CoreAudioDevice?.AudioEndpointVolume is AudioEndpointVolume volume)
            {
                volume.MasterVolumeLevelScalar = (float)e.NewValue;
            }
        }

        private void RemoveDeviceCallbacks()
        {
            client.DefaultDeviceChanged -= Client_DefaultDeviceChanged;
            client.DeviceAdded -= Client_DeviceAdded;
            client.DevicePropertyChanged -= Client_DevicePropertyChanged;
            client.DeviceStateChanged -= Client_DeviceStateChanged;
            client.DeviceRemoved -= Client_DeviceRemoved;
        }

        private void AddDeviceCallbacks()
        {
            client.DefaultDeviceChanged += Client_DefaultDeviceChanged;
            client.DeviceAdded += Client_DeviceAdded;
            client.DevicePropertyChanged += Client_DevicePropertyChanged;
            client.DeviceStateChanged += Client_DeviceStateChanged;
            client.DeviceRemoved += Client_DeviceRemoved;
        }

        private void Client_DeviceRemoved(object? sender, DeviceNotificationEventArgs e)
        {
            InitializeDevices();
        }

        private void Client_DeviceStateChanged(object? sender, DeviceStateChangedEventArgs e)
        {
            //InitializeDevices();
        }

        private void Client_DevicePropertyChanged(object? sender, DevicePropertyChangedEventArgs e)
        {
            //InitializeDevices();
        }

        private void Client_DeviceAdded(object? sender, DeviceNotificationEventArgs e)
        {
            InitializeDevices();
        }

        private void Client_DefaultDeviceChanged(object? sender, DefaultDeviceChangedEventArgs e)
        {
            switch (e.DataFlow)
            {
                case DataFlow.Render:
                    if (e.TryGetDevice(out MMDevice? newDevice) && newDevice != null)
                    {
                        InitializeDevices(newSpeakerDevice: newDevice.ToDevice());
                        return;
                    }
                    break;
                case DataFlow.Capture:
                    if (e.TryGetDevice(out newDevice) && newDevice != null)
                    {
                        InitializeDevices(newMicrophoneDevice: newDevice.ToDevice());
                        return;
                    }
                    break;
                case DataFlow.All:
                    break;
            }
            InitializeDevices();
        }

        private void ContentControl_MouseEnter(object sender, MouseEventArgs e)
        {
            UpdateCloseImageSource(sender, "CloseImageMouseOver");
        }

        private static void UpdateCloseImageSource(object sender, string resourceName)
        {
            if (sender is ContentControl contentControl)
            {
                contentControl.Content = Application.Current.Resources[resourceName];
            }
        }

        private void ContentControl_MouseLeave(object sender, MouseEventArgs e)
        {
            UpdateCloseImageSource(sender, "CloseImage");
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            Topmost = true;
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            Topmost = false;
        }

        private void ContentControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Close();
        }
    }

    public record Device(string Name, string Id, MMDevice CoreAudioDevice);

    public static class DeviceExtensions
    {
        public static List<Device> ToDevices(this MMDeviceCollection deviceCollection)
        {
            return deviceCollection.Select(d => d.ToDevice()).ToList();
        }

        public static Device ToDevice(this MMDevice device) => new(device.DeviceFriendlyName, device.ID, device);
    }
}