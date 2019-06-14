using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO.Ports;
using System.Reflection;
using System.Resources;
using System.Text;
using Microsoft.PointOfService;
using Microsoft.PointOfService.BaseServiceObjects;
using Microsoft.Win32;

namespace Masnavi
{
    #region ExampleScanner Class
    [ServiceObject(DeviceType.Scanner,
                    "Yasna Scanner",
                   "Service object for Yasna Scanner", 13, 68)]
    public class YasnaScanner : ScannerBase
    {
        private SerialPort _serialPort;
        private ResourceManager rm;

        public YasnaScanner()
        {
            try
            {
                // Initialize ResourceManager for loading localizable strings
                rm = new ResourceManager("Masnavi.Strings", Assembly.GetExecutingAssembly());
                var desc = rm.GetString("IDS_SCANNER_DEVICE_DESCRIPTION");

                DevicePath = desc;
                // Initialize device properties
                Properties.DeviceDescription = desc;
                SetStatisticValue(StatisticManufacturerName, "Yasna System");
                SetStatisticValue(StatisticManufactureDate, "2019-06-14");
                SetStatisticValue(StatisticModelName, "Yasna Scanner");
                SetStatisticValue(StatisticMechanicalRevision, "13.68");
                SetStatisticValue(StatisticInterface, "USB");
                _serialPort = new SerialPort
                {
                    PortName = GetPortFromRegistry(),
                    BaudRate = GetBaudRateFromRegistry(),
                    ReadTimeout = 500,
                    WriteTimeout = 500
                };
                _serialPort.DataReceived += SerialPort_DataReceived;

            }
            catch (Exception ex)
            {
                Logger.Error("YasnaScanner", ex.Message, ex);

            }

        }


        private string checkhealthtext;
        public override string CheckHealthText
        {
            get
            {
                // Verify that device is open
                VerifyState(false, false);

                return checkhealthtext;
            }
        }


        public override string CheckHealth(HealthCheckLevel level)
        {
            // Verify that device is open, claimed and enabled
            VerifyState(false, true);

            // TODO: check the health of the device and return a descriptive string 

            // Cache result in the CheckHealthText property
            checkhealthtext = "Bagher Scanner is OK!";
            return checkhealthtext;
        }

        public override DirectIOData DirectIO(int command, int data, object obj)
        {
            // Verify that device is open
            VerifyState(false, false);

            return new DirectIOData(data, obj);
        }

        public override void Open()
        {
            // Device State checking done in base class
            base.Open();

            // Initialize the CheckHealthText property to an empty string
            checkhealthtext = "";

            try
            {
                // Set values for common statistics

                DeviceEnabled = true;

            }
            catch (Exception ex)
            {
                Logger.Error("Open", ex.Message, ex);
            }

            // Create instance of USB reader class
            //hidReader = new HidReader(DeviceName, DevicePath, OnDataScanned);
            //hidReader.ThreadExceptionEvent += new HidReader.ThreadExceptionEventHandler(hidReader_ThreadExceptionEvent);
        }

        public override void Close()
        {
            base.Close();
            try
            {
                // Set values for common statistics

                DeviceEnabled = false;
            }
            catch (Exception ex)
            {
                Logger.Error("Close", ex.Message, ex);
            }

        }
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {

            try
            {
                // Ignore input if we're in the Error state

                if (State == ControlState.Error)
                {
                    Logger.Warn(DeviceName, "Input data ignored because device is in the Error state.");
                    return;
                }

                SerialPort sp = (SerialPort)sender;
                //string indata = sp.ReadExisting();
                int bytes = sp.BytesToRead;
                byte[] respBuffer = new byte[bytes];
                sp.Read(respBuffer, 0, bytes);
                if (bytes > 1)
                {
                    var labelArray = new byte[bytes - 1];
                    Array.Copy(respBuffer,0, labelArray,0, bytes-1);
                    GoodScan(respBuffer, BarCodeSymbology.Ean13S, labelArray);

                }
                else
                {
                    GoodScan(respBuffer, BarCodeSymbology.Ean13S, respBuffer);

                }

                /*if ((int)data[1] < 5)
                {
                    FailedScan();
                }
                else
                {
                    byte[] b = new byte[(int)data[1] + 1];
                    for (int i = 0; i <= (int)data[1]; i++)
                        b[i] = data[i];

                    GoodScan(b);
                }*/
            }
            catch (Exception ex)
            {
                Logger.Error("SerialPort_DataReceived", ex.Message, ex);
            }
        }

        private int GetBaudRateFromRegistry()
        {
            var retValue = 9600;
            try
            {
                RegistryKey myKey = Registry.CurrentUser.OpenSubKey(@"Software\YasnaSystem", true);
                if (myKey != null)
                {
                    retValue = Convert.ToInt32(myKey.GetValue("YSB", "9600"));
                    myKey.Close();
                }
            }
            catch (Exception )
            {
               //ignore
            }
            return retValue;

        }

        private string GetPortFromRegistry()
        {
            var retValue = "COM10";
            try
            {
                RegistryKey myKey = Registry.CurrentUser.OpenSubKey(@"Software\YasnaSystem", true);
                if (myKey != null)
                {
                    retValue = Convert.ToString(myKey.GetValue("YSP", "COM10"));
                    myKey.Close();
                }
            }
            catch (Exception )
            {
                //ignore
            }
            return retValue;
        }


        public override bool DeviceEnabled
        {
            // Device State checking done in base class
            get => base.DeviceEnabled;
            set
            {
                if (value != base.DeviceEnabled)
                {
                    // Call base class first because it will handle cases such as the
                    // device isn't claimed etc.
                    base.DeviceEnabled = value;

                    // Start/Stop reading from the device
                    if (value == false)
                    {
                        if (_serialPort != null && _serialPort.IsOpen)
                        {
                            _serialPort.Close();
                        }
                    }
                    else
                    {
                        try
                        {
                            if (_serialPort != null && !_serialPort.IsOpen)
                            {
                                _serialPort.Open();
                            }
                        }
                        catch (Exception e)
                        {
                            // disable device
                            base.DeviceEnabled = false;

                            // rethrow PosControlExceptions
                            if (e is PosControlException)
                                throw;

                            // Wrap other exceptions in PosControlExceptions
                            throw new PosControlException(rm.GetString("IDS_UNABLE_TO_ENABLE_DEVICE"), ErrorCode.Failure, e);
                        }
                    }
                }
            }
        }



        /*static private BarCodeSymbology GetSymbology(byte b1, byte b2, byte b3)
        {
            if (b1 == 0 && b3 == 11)
            {
                switch (b2)
                {
                    case 10:
                        return BarCodeSymbology.Code39;
                    case 13:
                        return BarCodeSymbology.Itf;
                    case 14:
                        return BarCodeSymbology.Codabar;
                    case 24:
                        return BarCodeSymbology.Code128;
                    case 25:
                        return BarCodeSymbology.Code93;
                    case 37:
                        return BarCodeSymbology.Ean128;
                    case 255:
                        return BarCodeSymbology.Gs1DataBar;
                    default:
                        break;
                }

            }
            else if (b2 == 0 && b3 == 0)
            {
                switch (b1)
                {
                    case 13:
                        return BarCodeSymbology.Upca;
                    case 22:
                        return BarCodeSymbology.EanJan13;
                    case 12:
                        return BarCodeSymbology.EanJan8;
                    default:
                        break;
                }
            }

            return BarCodeSymbology.Other;

        }

        override protected byte[] DecodeScanDataLabel(byte[] scanData)
        {
            int i;
            int len = 0;

            // Get length of label data
            for (i = 5; i < (int)scanData[1] && (int)scanData[i] > 31; i++)
                len++;

            // Copy label data into buffer
            byte[] label = new byte[len];
            len = 0;
            for (i = 5; i < (int)scanData[1] && (int)scanData[i] > 31; i++)
                label[len++] = scanData[i];

            return label;
        }

        override protected BarCodeSymbology DecodeScanDataType(byte[] scanData)
        {
            int i;
            for (i = 5; i < (int)scanData[1] && (int)scanData[i] > 31; i++) { }

            // last 3 (or 1) bytes indicate symbology
            if (i + 2 <= (int)ScanData[1])
                return GetSymbology(ScanData[i], ScanData[i + 1], ScanData[i + 2]);
            else
                return GetSymbology(ScanData[i], 0, 0);
        }*/
    }

    #endregion
}
