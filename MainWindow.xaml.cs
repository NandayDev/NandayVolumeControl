using CoreAudio;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NandayVolumeControl
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MMDeviceEnumerator deviceEnumerator = new(Guid.NewGuid());
        private readonly MMNotificationClient client;
        private Device? currentSpeaker = null;
        private Device? currentMicrophone = null;

        public MainWindow()
        {
            InitializeComponent();
            client = new(deviceEnumerator);
            InitializeDevices();
            MouseDown += DragOnMouseDown;
            KeyDown += ExitOnEscPressed;
            Closing += OnClosing;
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
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void InitializeDevices(Device? newSpeakerDevice = null, Device? newMicrophoneDevice = null)
        {
            Dispatcher.Invoke(delegate
            {
                speakersComboBox.SelectionChanged -= OnComboBoxSelectionChanged;
                microphoneComboBox.SelectionChanged -= OnComboBoxSelectionChanged;
                RemoveDeviceCallbacks();
                InitializeDeviceList(DataFlow.Render, speakersComboBox, newSpeakerDevice);
                InitializeDeviceList(DataFlow.Capture, microphoneComboBox, newMicrophoneDevice);
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

        private void InitializeDeviceList(DataFlow dataFlow, ComboBox comboBox, Device? newDefaultDevice)
        {
            var nAudioDevices = deviceEnumerator.EnumerateAudioEndPoints(dataFlow, DeviceState.Active).ToDevices();
            comboBox.ItemsSource = nAudioDevices;
            var defaultDevice = newDefaultDevice ?? deviceEnumerator.GetDefaultAudioEndpoint(dataFlow, Role.Console).ToDevice();
            comboBox.SelectedIndex = nAudioDevices.IndexOf(nAudioDevices.First(d => d.Id == defaultDevice.Id));
            if (dataFlow == DataFlow.Render)
            {
                currentSpeaker = defaultDevice;
            }
            else
            {
                currentMicrophone = defaultDevice;
            }
        }

        private void OnComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            deviceEnumerator.SetDefaultAudioEndpoint((e.AddedItems[0] as Device)!.CoreAudioDevice);
        }

        private static void InitializeSlider(Device device, Slider slider)
        {
            slider.Value = device.CoreAudioDevice.AudioEndpointVolume!.MasterVolumeLevelScalar;
            slider.ValueChanged += delegate (object o, RoutedPropertyChangedEventArgs<double> value)
            {
                device.CoreAudioDevice.AudioEndpointVolume.MasterVolumeLevelScalar = (float)value.NewValue;
            };
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
            InitializeDevices();
        }

        private void Client_DevicePropertyChanged(object? sender, DevicePropertyChangedEventArgs e)
        {
            InitializeDevices();
        }

        private void Client_DeviceAdded(object? sender, DeviceNotificationEventArgs e)
        {
            InitializeDevices();
        }

        private void Client_DefaultDeviceChanged(object? sender, DefaultDeviceChangedEventArgs e)
        {
            InitializeDevices();
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