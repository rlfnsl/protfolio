using UnityEngine;
using System;
using System.Collections;
using System.IO.Ports;
using System.Threading;
using System.Collections.Generic;
using System.Xml;
using System.IO;
using UnityEngine.SceneManagement;
using System.Drawing.Imaging;

public class SerialPortUnit : MonoBehaviour
{
    public enum SerialCode
    {
        ScanStart = 0x16,   // 스캔 시작
        Discard = 0x06,   // 무시
        RestoreFactoryDefaults,

        // 유저 세팅 정보 저장방법
        StartSetting,
        _232Setting,
        SaveUserDefault,
        TurnOffSetting,
        //
        ManualMode,
        NoLimits,

        RestoreUserDefaults,
    }

    SerialPort _SerialPort;
    string _PortName = "COM3";
    int _BaudRate = 9600;
    int _DataBits = 8;
    StopBits _StopBits = StopBits.One;
    Parity _Parity = Parity.None;
    bool _IsConnected = false;

    const int _ClientIOMajorVersion = 1;
    const int _ClientIOMinorVersion = 0;
    int _SystemIOMajorVersion = 0;
    int _SystemIOMinorVersion = 0;

    // Buffer
    const int _ReadBufferSize = 128;
    const int _SendBufferSize = 8;
    byte[] _ReadBuffer = new byte[_ReadBufferSize];
    byte[] _WriteBuffer = new byte[_SendBufferSize];
    List<WirteQueue> _Queue = new List<WirteQueue>();
    string _ReadLastError = "";
    string readData = "";
    string readPeiceData = ""; // 조각난 데이터
    string lastReadData = "";
    public bool readType = false;
    public string serialName = "SerialReader1";

    //ButtonBuffer _ButtonBuffer = new ButtonBuffer();
    //ButtonBuffer _ThreadButtonBuffer = new ButtonBuffer();
    //int _ReadBufferIndex = 0;

    // Thread
    Thread _SerialThread;
    bool _ThreadRunning;
    float _SleepRemain = 0f;
    public float writeLastTime = 0f;

    class WirteQueue
    {
        public float delay = 0f;
        public byte[] buffer = null;
    }

    void Awake()
    {
        //OpenSerialPort();
    }

    void Update()
    {

        CheckQueueWriteBuffer();
    }

    void CheckQueueWriteBuffer()
    {
        _SleepRemain -= Time.deltaTime;
        if (_SleepRemain <= 0 && _Queue.Count > 0)
        {
            try
            {
                WirteQueue queue = _Queue[0];
                _Queue.RemoveAt(0);
                _SerialPort.Write(queue.buffer, 0, 5);
                LogWriteBuffer(queue.buffer);
                _SleepRemain = queue.delay;
            }
            catch (Exception e)
            {
                Debug.Log(e);
            }
        }
    }

    void OnApplicationQuit()
    {
        StopThread();
        CloseSerialPort();
    }

    void LoadXML()
    {
        XmlDocument xmlDoc = new XmlDocument();

        xmlDoc.Load(Application.streamingAssetsPath + "/xml/SerialConfig.xml");

        XmlNode root = xmlDoc.SelectSingleNode("SerialConfig");

        // SerialPort
        XmlNode node = root.SelectSingleNode(serialName);
        _PortName = node.Attributes["portName"].Value;
        _BaudRate = int.Parse(node.Attributes["baudRate"].Value);
        _Parity = (Parity)int.Parse(node.Attributes["parity"].Value);
        _DataBits = int.Parse(node.Attributes["dataBits"].Value);
        _StopBits = (StopBits)int.Parse(node.Attributes["stopBits"].Value);
    }

    public void OpenSerialPort()
    {
        LoadXML();
        try
        {
            _SerialPort = new SerialPort(_PortName, _BaudRate, _Parity, _DataBits, _StopBits);
            _SerialPort.ReadTimeout = 1;
            _SerialPort.WriteTimeout = 14;

            _SerialPort.Open();
            Debug.Log("SerialPort Open() : " + _SerialPort.IsOpen.ToString());

            if (_SerialPort.IsOpen)
            {
                StartThread();
                //Write(SerialCode.TryConnect);
            }
            else
            {
                Debug.Log("연결안됨");
            }
        }
        catch (Exception e)
        {
            Debug.Log(e);
        }

        // 		_SerialPort.DataReceived  += DataReceivedHandler;
        // 		_SerialPort.ErrorReceived += ErrorReceivedHandler;
    }

    void CloseSerialPort()
    {
        if (_SerialPort.IsOpen)
        {
            _SerialPort.Close();
            Debug.Log("SerialPort Close()");
        }
    }

    void StartThread()
    {
        try
        {
            _ThreadRunning = true;
            //_SerialThread = new Thread(ThreadLoop);
            _SerialThread = new Thread(new ThreadStart(ThreadLoop));
            _SerialThread.Start();
        }
        catch (Exception e)
        {
            Debug.Log(e);
        }
    }

    void StopThread()
    {
        if (_SerialThread != null)
        {
            _ThreadRunning = false;
            Thread.Sleep(100);
            _SerialThread.Abort();
            _SerialThread = null;
            Debug.Log("Stop SerialThread");
        }
    }

    void ThreadLoop()
    {
        Debug.Log("Start SerialThreadLoop");
        while (_ThreadRunning)
        {
            ReadBuffer();
            Thread.Sleep(10);
        }
    }

    void ReadBuffer()
    {
        if (!_SerialPort.IsOpen)
            return;

        try
        {
            while (_ThreadRunning)
            {
                if (_SerialPort.BytesToRead > 0)
                {
                    int cnt = _SerialPort.BytesToRead;
                    _SerialPort.Read(_ReadBuffer, 0, _ReadBufferSize);
                    string debug = "";
                    string debug2 = "";
                    for (int i = 0; i < cnt; i++)
                    {
                        if (0x06 == _ReadBuffer[i])
                        {
                            //Debug.Log($"ReadBuffer pass {_ReadBuffer[i]} {_ReadBuffer[i].ToString("X2")}");
                        }
                        else
                        {
                            debug += (char)_ReadBuffer[i];
                            debug2 += _ReadBuffer[i].ToString("X2");
                        }

                    }
                    //Debug.Log($"cnt:{cnt} {_ReadBuffer} buffer:{debug} {debug == debug.Trim()} trim:{debug.Trim()} len:{debug.Trim().Length} 0x:{debug2}");
                    if (debug.Trim() != "")
                    {
                        //Debug.Log($"readPeiceData pre:{readPeiceData}");
                        readPeiceData += debug.Trim();
                        //Debug.Log($"readPeiceData after:{readPeiceData}");
                        if (readPeiceData.Contains("MC") && readPeiceData.Length >= 9) // QRT110111
                        {
                            readData = readPeiceData;
                            readPeiceData = "";
                        }
                        else if (!readPeiceData.Contains("MC") && readPeiceData.ToUpper().Contains("QR") && readPeiceData.Length >= 8) // QREEEE01
                        {
                            readData = readPeiceData;
                            readPeiceData = "";
                        }
                        else
                        {
                            //Debug.LogWarning($"else {readData} {readPeiceData}");
                        }
                    }
                }

                if (readType)
                {
                    // https://docs.microsoft.com/ko-kr/dotnet/api/system.io.ports.serialport.readexisting?view=dotnet-plat-ext-6.0
                    // 인코딩을 기준으로 SerialPort 개체의 스트림 및 입력 버퍼 모두에서 즉시 사용할 수 있는 모든 바이트를 읽습니다.
                    //readData = _SerialPort.ReadExisting().Trim();
                }
                else
                {
                    // https://docs.microsoft.com/ko-kr/dotnet/api/system.io.ports.serialport.readline?view=dotnet-plat-ext-6.0
                    // 입력 버퍼에서 NewLine 값까지 읽습니다.
                    //readData = _SerialPort.ReadLine().Trim();
                }

                //lastReadTime = Time.time;
                LogReadBuffer();
                //ProcessReadBuffer();
                ResetReadBuffer();
            }
        }
        catch (Exception e)
        {
            Debug.Log($"{e}");
            //if (_ReadLastError != "ReadBuffer\n" + e.ToString())
            //{
            _ReadLastError = "ReadBuffer\n" + e.ToString();
            //}
        }
    }

    void ResetReadBuffer()
    {
        //_ReadBufferIndex = 0;
        Array.Clear(_ReadBuffer, 0, _ReadBuffer.Length);
    }

    void ProcessReadBuffer()
    {
        if (_ReadBuffer[4] != 0xEE)
            return;

        SerialCode code = (SerialCode)_ReadBuffer[0];

        switch (code)
        {
            case SerialCode.Discard: // 무시 (0x06)
                UpdateConnection();
                break;
        }
    }

    void ReadBuffer2()
    {
        if (!_SerialPort.IsOpen)
            return;

        try
        {
            _ReadBuffer[0] = (byte)_SerialPort.ReadByte();
            Debug.Log(string.Format("{0:X2}", _ReadBuffer[0]));
        }
        catch { }
    }

    void WriteBuffer(SerialCode code, byte data1, byte data2, byte data3, byte data4, byte data5, byte data6, float delay = 0)
    {
        if (_SerialPort.IsOpen)
        {
            _WriteBuffer[0] = (byte)code;
            _WriteBuffer[1] = data1;
            _WriteBuffer[2] = data2;
            _WriteBuffer[3] = data3;
            _WriteBuffer[4] = data4;
            _WriteBuffer[5] = data5;
            _WriteBuffer[6] = data6;
            _WriteBuffer[7] = 0x2E;
            try
            {
                _SerialPort.Write(_WriteBuffer, 0, _SendBufferSize);
            }
            catch (TimeoutException e)
            {
                //LogFile.WriteData(string.Format("writebuffer {0} {1:X2} {2:X2} {3:X2} {4:X2} {5}", code, (byte)code, data1, data2, data3, delay));
                //LogFile.WriteData(e.ToString());
                Debug.Log(e.ToString());
            }
            catch (IOException e)
            {
                //LogFile.WriteData(string.Format("writebuffer {0} {1:X2} {2:X2} {3:X2} {4:X2} {5}", code, (byte)code, data1, data2, data3, delay));
                //LogFile.WriteData(e.ToString());
                Debug.Log(e.ToString());
            }
            catch (Exception e)
            {
                Debug.Log(e);
            }
            LogWriteBuffer();

            //WirteQueue queue = new WirteQueue();
            //queue.buffer = new byte[_BufferSize];
            //queue.buffer[0] = (byte)code;
            //queue.buffer[1] = data1;
            //queue.buffer[2] = data2;
            //queue.buffer[3] = data3;
            //queue.buffer[4] = 0xEE;
            //queue.delay = delay;
            //_Queue.Add(queue);
        }
    }

    void WriteBuffer(byte code, byte data1, byte data2, byte data3, byte data4, byte data5, byte data6, float delay = 0)
    {
        if (_SerialPort.IsOpen)
        {
            _WriteBuffer[0] = code;
            _WriteBuffer[1] = data1;
            _WriteBuffer[2] = data2;
            _WriteBuffer[3] = data3;
            _WriteBuffer[4] = data4;
            _WriteBuffer[5] = data5;
            _WriteBuffer[6] = data6;
            _WriteBuffer[7] = 0x2E;
            try
            {
                _SerialPort.Write(_WriteBuffer, 0, _SendBufferSize);
            }
            catch (TimeoutException e)
            {
                //LogFile.WriteData(string.Format("writebuffer {0} {1:X2} {2:X2} {3:X2} {4:X2} {5}", code, (byte)code, data1, data2, data3, delay));
                //LogFile.WriteData(e.ToString());
                Debug.Log(e.ToString());
            }
            catch (IOException e)
            {
                //LogFile.WriteData(string.Format("writebuffer {0} {1:X2} {2:X2} {3:X2} {4:X2} {5}", code, (byte)code, data1, data2, data3, delay));
                //LogFile.WriteData(e.ToString());
                Debug.Log(e.ToString());
            }
            catch (Exception e)
            {
                Debug.Log(e);
            }
            LogWriteBuffer();
        }
    }

    public void Write(SerialCode code, byte data1 = 0x01, byte data2 = 0, byte data3 = 0, float delay = 0)
    {
        writeLastTime = Time.time;
        switch (code)
        {
            case SerialCode.ScanStart:
                WriteBuffer(code, 0x42, 0x65, 0x52, 0x65, 0x51, 0x62, delay);
                break;

            case SerialCode.RestoreFactoryDefaults:
                WriteBuffer(0x16, 0x42, 0x65, 0x51, 0x65, 0x43, 0x65, delay);
                break;
            case SerialCode.RestoreUserDefaults:
                WriteBuffer(0x16, 0x42, 0x65, 0x51, 0x65, 0x45, 0x65, delay);
                break;


            case SerialCode.StartSetting:
                WriteBuffer(0x16, 0x52, 0x61, 0x5A, 0x64, 0x4E, 0x61, delay);
                break;
            case SerialCode._232Setting:
                WriteBuffer(0x16, 0x56, 0x62, 0x5A, 0x63, 0x4E, 0x63, delay);
                break;
            case SerialCode.SaveUserDefault:
                WriteBuffer(0x16, 0x55, 0x61, 0x51, 0x64, 0x57, 0x61, delay);
                break;

            case SerialCode.ManualMode:
                // 4. Manual mode : 16 56 62 42 65 4A 62 2E
                WriteBuffer(0x16, 0x56, 0x62, 0x42, 0x65, 0x4A, 0x62, delay);
                break;
            case SerialCode.NoLimits:
                // 5. No limits (read명령어 후에 바코드 인식 안했을 때 '제한없이' 대기) :
                // 16 55 61 5A 63 43 62 2E
                WriteBuffer(0x16, 0x55, 0x61, 0x5A, 0x63, 0x43, 0x62, delay);
                break;


            case SerialCode.TurnOffSetting:
                WriteBuffer(0x16, 0x52, 0x61, 0x5A, 0x64, 0x58, 0x61, delay);
                break;

            default:
                //WriteBuffer(code, data1, data2, data3);
                break;
        }
    }

    string GetCodeMessage(SerialCode code)
    {
        string msg = "default";
        switch (code)
        {
            case SerialCode.Discard: // 무시 (0x06)
                msg = "무시 (0x06)";
                break;
        }
        return msg;
    }

    string GetTime()
    {
        return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
    }

    public string GetLastReadData()
    {
        return lastReadData;
    }
    public string GetLastReadDataAndClear()
    {
        string value = lastReadData;
        lastReadData = "";
        return value;
    }
    void LogReadBuffer()
    {
        if (readData == "" || readData.Length < 2)
        {
            if (readData != "")
                Debug.Log($"LogReadBuffer delete {readData}");
            return;
        }
        lastReadData = readData;
        //string msg = GetCodeMessage((SerialCode)_ReadBuffer[0]);
        Debug.Log($"LogReadBuffer {readData}");
        readData = "";
    }

    void LogWriteBuffer()
    {
        string msg = GetCodeMessage((SerialCode)_WriteBuffer[0]);
        string value = "";
        for (int i = 0; i < _WriteBuffer.Length; i++)
        {
            value += $"{_WriteBuffer[i]:X2} ";
        }
        Debug.LogWarning(string.Format("Serial Write1 : {0} {1}", value, msg));

    }

    void LogWriteBuffer(byte[] buffer)
    {
        string msg = GetCodeMessage((SerialCode)buffer[0]);
        string value = "";
        for (int i = 0; i < buffer.Length; i++)
        {
            value += $"{buffer[i]:X2} ";
        }
        Debug.LogWarning(string.Format("Serial Write2 : {0} {1}", value, msg));
    }

    public bool IsConnected()
    {
        return _IsConnected;
    }

    void UpdateConnection()
    {
        if (_ReadBuffer[0] == 0x06)
            _IsConnected = true;
    }

    public bool FirmwareVersionOK()
    {
        if (_SystemIOMajorVersion == _ClientIOMajorVersion &&
            _SystemIOMinorVersion == _ClientIOMinorVersion)
            return true;
        else
            return false;
    }
}