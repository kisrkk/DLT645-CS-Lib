using System;
using System.Diagnostics;
using System.IO.Ports; 
using System.Windows;

class DLT645
{
    public SerialPort serialPort { get; set; }
    public bool kDebug = false;
    public static string command_to_send = "";
    public static string res_from_meter = "";
    public DLT645(SerialPort _serialPort,bool _kDebug = false)
    {
        serialPort = _serialPort;
        kDebug = _kDebug;
    } 
    public float ReadTotalEnergy(string meterID)
    { 
        ReadRequest(meterID);
        int availableBytes = serialPort.BytesToRead;
        if (availableBytes <= 0)
        {
            DateTime _startTime = DateTime.Now;
            TimeSpan _timeout = TimeSpan.FromMilliseconds(380); 
            while ( (DateTime.Now - _startTime) < _timeout)
            {;}// Wait for data incoming
        }
        availableBytes = serialPort.BytesToRead;
        byte[] responseData = new byte[availableBytes];
        int bytesRead = 0;
        int totalBytesExpected = responseData.Length;
        DateTime startTime = DateTime.Now;
        TimeSpan timeout = TimeSpan.FromMilliseconds(380);

        while (bytesRead < totalBytesExpected && DateTime.Now - startTime < timeout)
        { 
            if (availableBytes > 0)
            {
                int bytesToRead = Math.Min(availableBytes, totalBytesExpected - bytesRead);
                bytesRead += serialPort.Read(responseData, bytesRead, bytesToRead);
            }
        }

        if (bytesRead != totalBytesExpected)
        {
            if (kDebug) {
                MessageBox.Show(("Timeout or incomplete data."));
            }
        }
        
        float energyValue = ParseEnergyValue(meterID, responseData);
        return energyValue;
    }

    private byte xStringToHex(String hexString)
    {
        return Convert.ToByte(hexString, 16);
    }
    private void ReadRequest(string meterID)
    {
        if (serialPort == null)
        {
            return;
        }
        byte[] command = new byte[20] {
            0xFE, 0xFE, 0xFE, 0xFE, /* Clear Communication */
            0x68, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, /* Meter ID */
            0x68, 0x11, 0x04, 0x33, 0x33, 0x33, 0x33, /* Command Frame */
            0x00, 0x16 /* CRC and Stop */
        };

        Debug.WriteLine(meterID);
        for (int i = 1; i < 6; i++)
        {
            string input = meterID.Substring((i - 1) * 2, 2); 
            command[10 - i] = xStringToHex(input);
        }

        byte sumCRC = 0;
        for (int i = 4; i <= 17; i++)
        {
            sumCRC += command[i];
        }
        command[18] = (byte)(sumCRC & 0xFF);
        string res = "";
        foreach (byte b in command)
        {
            res += b.ToString("X2"); 
            res += "\n";
            command_to_send += b.ToString("X2");
            command_to_send += " ";
        }
        serialPort.Write(command, 0, command.Length);
        serialPort.BaseStream.Flush();
    }

    private float ParseEnergyValue(string meterID,byte[] responseData)
    {
        //FE FE FE FE //3
        //68 87 53 90 11 23 00 //10
        //68 91 08 33 33 33 33 33 33 33 33 //21
        //9F 16
        //
        string res = "";
        foreach (byte b in responseData)
        {
            res += b.ToString("X2");
            res += "\n";
            res_from_meter += b.ToString("X2");
            res_from_meter += " ";
        }
        
        byte sumCRC = 0;
        for (int i = 4; i <= 21; i++)
        {
            sumCRC += responseData[i];
        }
        byte callback_crc = (byte)(sumCRC & 0xFF);
        if (callback_crc != responseData[22]) {
            string err = callback_crc.ToString("X2") + "\n";
            if (kDebug)
            {
                MessageBox.Show(err, "Invaild CRC");
            }
            return -1.0f;
        }
        string callback_meter_id = "";
        for (int i = 9; i > 4; i--) {
            callback_meter_id += responseData[i].ToString("X2");
        }

        if (callback_meter_id != meterID) {
            if (kDebug)
            {
                MessageBox.Show(callback_meter_id, "Invaild Callback MeterID");
            }
            return -2.0f;
        }

        string energy = "";
        for (int i = 17; i >= 14; i--)
        {
            if ((responseData[i] - 0x33) < 10)
            {
                energy += "0";
            }
            energy += ((responseData[i] - 0x33)).ToString("X2"); 
        }

        return float.Parse(energy) / 100; // 000000.00
    }
}
