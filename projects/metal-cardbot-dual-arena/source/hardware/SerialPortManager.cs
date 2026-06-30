using UnityEngine;
using System;
using System.Collections;
using System.IO.Ports;
using System.Threading;
using System.Collections.Generic;
using System.Xml;
using System.IO;
using UnityEngine.SceneManagement;

public class SerialPortManager : MonoBehaviour
{
    public static SerialPortManager Instance = null;

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
    const int _BufferSize = 5;
    byte[] _ReadBuffer = new byte[_BufferSize];
    byte[] _WriteBuffer = new byte[_BufferSize];
    List<WirteQueue> _Queue = new List<WirteQueue>();
    string _ReadLastError = "";

    ButtonBuffer _ButtonBuffer = new ButtonBuffer();
    ButtonBuffer _ThreadButtonBuffer = new ButtonBuffer();
    int _ReadBufferIndex = 0;

    // Thread
    Thread _SerialThread;
    bool _ThreadRunning;

    CardOutFlag _CardOutFlag;
    CardStateFlag _CardStateFlag;
    int _Coin = 0;
    int _Credit = 0;
    int _ServiceCredit = 0;
    int _TestCoin = 0;

    bool _ManagerMode = false;
    bool _CoinTestMode = false;
    public bool IsCardTaking = false;
    float _SleepRemain = 0f;

    bool[] cardTakeDeviceCheck = new bool[2]; // 3개까지 가능하지만 일단 2개
    public float writeCardOutLastTime;
    public bool IsError;
    public SerialPortUnit serialPortReader0;
    public SerialPortUnit serialPortReader1;
    public CardoutQRChecker cardoutQRChecker0;
    public CardoutQRChecker cardoutQRChecker1;

    class WirteQueue
    {
        public float delay = 0f;
        public byte[] buffer = null;
    }

    void Awake()
    {
        if (Instance == null)
        {
            cardTakeDeviceCheck[0] = true;
            cardTakeDeviceCheck[1] = true;
            Instance = this;
            DontDestroyOnLoad(gameObject);
            OpenSerialPort();

            var go = new GameObject();
            serialPortReader0 = go.AddComponent<SerialPortUnit>();
            serialPortReader0.serialName = "SerialReader1";
            serialPortReader0.readType = true;
            serialPortReader0.OpenSerialPort();
            cardoutQRChecker0 = go.AddComponent<CardoutQRChecker>();
            cardoutQRChecker0.deviceNum = 0;
            cardoutQRChecker0.serial = serialPortReader0;
            Gamemanager.instance.unit1 = go.GetComponent<CardoutQRChecker>();
            go.transform.SetParent(transform);
            go.name = serialPortReader0.serialName;

            go = new GameObject();
            serialPortReader1 = go.AddComponent<SerialPortUnit>();
            serialPortReader1.serialName = "SerialReader2";
            serialPortReader1.readType = true;
            serialPortReader1.OpenSerialPort();
            cardoutQRChecker1 = go.AddComponent<CardoutQRChecker>();
            Gamemanager.instance.unit2 = go.GetComponent<CardoutQRChecker>();
            cardoutQRChecker1.deviceNum = 1;
            cardoutQRChecker1.serial = serialPortReader1;
            go.transform.SetParent(transform);
            go.name = serialPortReader1.serialName;

        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        //CheckQueueWriteBuffer();
        UpdateButtonBuffers();
        //CheckButtons();
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
        ButtonLED_AllOff();
        StopThread();
        CloseSerialPort();
    }
    void LoadXML()
    {
        XmlDocument xmlDoc = new XmlDocument();

        xmlDoc.Load(Application.streamingAssetsPath + "/xml/SerialConfig.xml");

        XmlNode root = xmlDoc.SelectSingleNode("SerialConfig");

        // SerialPort
        XmlNode node = root.SelectSingleNode("SerialPort");
        _PortName = node.Attributes["portName"].Value;
        _BaudRate = int.Parse(node.Attributes["baudRate"].Value);
        _Parity = (Parity)int.Parse(node.Attributes["parity"].Value);
        _DataBits = int.Parse(node.Attributes["dataBits"].Value);
        _StopBits = (StopBits)int.Parse(node.Attributes["stopBits"].Value);
    }
    void OpenSerialPort()
    {
        LoadXML();
        try
        {
            _SerialPort = new SerialPort(_PortName, _BaudRate, _Parity, _DataBits, _StopBits);
            _SerialPort.ReadTimeout = 10;
            _SerialPort.WriteTimeout = 14;
            _SerialPort.Open();
            Debug.Log("SerialPort Open() : " + _SerialPort.IsOpen.ToString());

            if (_SerialPort.IsOpen)
            {
                StartThread();
                Write(SerialCode.TryConnect);
            }
            else
            {
                Debug.Log("시리얼포트 연결 실패");
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

    void ReadBuffer0()
    {
        if (!_SerialPort.IsOpen)
            return;

        Array.Clear(_ReadBuffer, 0, _ReadBuffer.Length);

        try
        {
            //_SerialPort.Read(_ReadBuffer, 0, _ReadBuffer.Length);
            for (int i = 0; i < _ReadBuffer.Length; ++i)
            {
                _ReadBuffer[i] = (byte)_SerialPort.ReadByte();
                if (_ReadBuffer[i] == 0xEE)
                    break;
            }
        }
        catch { }

        if (_ReadBuffer[0] + _ReadBuffer[1] + _ReadBuffer[2] + _ReadBuffer[3] + _ReadBuffer[4] == 0)
            return;

        LogReadBuffer();
        ProcessReadBuffer();
    }

    void ReadBuffer()
    {
        if (!_SerialPort.IsOpen)
            return;

        try
        {
            while (true)
            {
                _ReadBuffer[_ReadBufferIndex] = (byte)_SerialPort.ReadByte();
                if (_ReadBuffer[_ReadBufferIndex] == 0xEE)
                {
                    LogReadBuffer();
                    ProcessReadBuffer();
                    ResetReadBuffer();
                }
                else
                {
                    if ((++_ReadBufferIndex) >= _BufferSize)
                        ResetReadBuffer();
                }
            }
        }
        catch (Exception e)
        {
            if (_ReadLastError != "ReadBuffer\n" + e.ToString())
            {
                _ReadLastError = "ReadBuffer\n" + e.ToString();
                Debug.Log(e);
            }
        }
    }

    void ResetReadBuffer()
    {
        _ReadBufferIndex = 0;
        Array.Clear(_ReadBuffer, 0, _ReadBuffer.Length);
    }

    void ProcessReadBuffer()
    {
        if (_ReadBuffer[4] != 0xEE)
            return;

        SerialCode code = (SerialCode)_ReadBuffer[0];
        string log = code.ToString();

        switch (code)
        {
            case SerialCode.Connected: // 연결 완료 (0x02)
                UpdateConnection();
                log = "연결 완료 (0x02)";
                break;

            case SerialCode.ConnectOK:  // 연결 유지 (0x03)
                log = "연결 유지 (0x03)";
                break;

            case SerialCode.InputBill:  // 지폐 입력 (0x04)
                log = "지폐 입력 (0x04)";
                ProcessBill();
                break;

            case SerialCode.InputCoin:  // 코인 입력 (0x05)
                log = "코인 입력 (0x05)";
                ProcessCoin();
                break;

            case SerialCode.CardOutComplete: // 카드 배출완료 (0x09)
                log = "카드 배출완료 (0x09)";
                SetCardOutState(CardOutFlag.CardOutOK);
                break;

            case SerialCode.CardOutError:   // 카드 배출에러 (0x0A)
                log = "카드 배출 에러 (0x0A)";
                if (_ReadBuffer[1] == 0x01)
                {
                    //cardTakeDeviceCheck[0] = false;
                }
                else if (_ReadBuffer[1] == 0x02)
                {
                    //cardTakeDeviceCheck[1] = false;
                }
                else if (_ReadBuffer[1] == 0x03)
                {
                    //cardTakeDeviceCheck[2] = false;
                }
                SetCardOutState(CardOutFlag.CardJam);
                /* 타겟을 지정하게 변경되었기에 사용하지 않음
				// 카드가 걸리거나 없을 경우. 다른곳으로 배출하는 기능
                if (GetTakeCardDevideNum() >= 0)
                    TakeCard(GetTakeCardDevideNum());
				*/
                break;

            case SerialCode.NoCard:     // 카드 없음 (0x45)
                log = "카드 없음 (0x45)";
                SetCardOutState(CardOutFlag.NoCard);
                break;

            case SerialCode.CardOutOrJam:   // 카드 배출 완료 또는 걸림 (0x47)
                log = "카드 배출 완료 또는 걸림 (0x47)";
                if (_ReadBuffer[1] == 0x01)
                    SetCardOutState(CardOutFlag.CardOutOK);
                else if (_ReadBuffer[1] == 0x02)
                    SetCardOutState(CardOutFlag.CardJam);
                break;

            case SerialCode.CardRefilled:
                log = "CardRefilled";
                if (_ReadBuffer[1] == 0x01)
                    SetCardOutState(CardOutFlag.CardRefill);
                break;

            case SerialCode.CardState:
                log = "CardState";
                UpdateCardState();
                break;

            case SerialCode.ButtonInput:// 버튼 입력 (0x0B)
                log = "버튼 입력 (0x0B)";
                UpdateButtonState();
                break;

            case SerialCode.ManagerMode:// 관리 버튼 (0x11)
                log = "관리 버튼 (0x11)";
                UpdateManagementButton();
                break;

            case SerialCode.ServiceCoin:// 서비스 코인 (0x12)
                log = "서비스 코인 (0x12)";
                UpdateServiceButton();
                break;

            case SerialCode.FirmwareVer:
                log = "FirmwareVer";
                if (_ReadBuffer[1] == 0x54)
                {
                    _SystemIOMajorVersion = _ReadBuffer[2];
                    _SystemIOMinorVersion = _ReadBuffer[3];
                }
                break;
            default:
                IsError = true;
                log = "null";
                break;
        }
#if !Arcade
        Debug.Log(log);
#endif
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

    void WriteBuffer(SerialCode code, byte data1, byte data2, byte data3, float delay = 0)
    {
        if (_SerialPort.IsOpen)
        {
            _WriteBuffer[0] = (byte)code;
            _WriteBuffer[1] = data1;
            _WriteBuffer[2] = data2;
            _WriteBuffer[3] = data3;
            _WriteBuffer[4] = 0xEE;
            try
            {
                _SerialPort.Write(_WriteBuffer, 0, 5);
            }
            catch (TimeoutException e)
            {
                //LogFile.WriteData(string.Format("writebuffer {0} {1:X2} {2:X2} {3:X2} {4:X2} {5}", code, (byte)code, data1, data2, data3, delay));
                //LogFile.WriteData(e.ToString());
                Debug.Log("WriteBuffer " + e.ToString());
            }
            catch (IOException e)
            {
                //LogFile.WriteData(string.Format("writebuffer {0} {1:X2} {2:X2} {3:X2} {4:X2} {5}", code, (byte)code, data1, data2, data3, delay));
                //LogFile.WriteData(e.ToString());
                Debug.Log("WriteBuffer IOException\n" + e.ToString());
            }
            catch (Exception e)
            {
                Debug.Log(e);
            }
            LogWriteBuffer();

            WirteQueue queue = new WirteQueue();
            queue.buffer = new byte[_BufferSize];
            queue.buffer[0] = (byte)code;
            queue.buffer[1] = data1;
            queue.buffer[2] = data2;
            queue.buffer[3] = data3;
            queue.buffer[4] = 0xEE;
            queue.delay = delay;
            //_Queue.Add(queue);
        }
    }

    public void Write(SerialCode code, byte data1 = 0x01, byte data2 = 0, byte data3 = 0, float delay = 0)
    {
        switch (code)
        {
            case SerialCode.TryConnect:
            case SerialCode.CardState:
            case SerialCode.CounterCard:
            case SerialCode.CounterCoin:
            case SerialCode.DisableMoney:
            case SerialCode.EnableMoney:
            case SerialCode.IRLEDOn:
            case SerialCode.IRLEDOff:
            case SerialCode.GameStart:
            case SerialCode.GameEnd:
            case SerialCode.ReqFirmwareVer:
                WriteBuffer(code, 0x01, 0, 0, delay);
                break;

            case SerialCode.CardOutStart:
                writeCardOutLastTime = Time.time;
                ClearCardOutState();
                WriteBuffer(code, data1, 0, 0);
                break;

            case SerialCode.ReqCardState:
                ClearCardState();
                WriteBuffer(code, 0x01, 0, 0);
                break;

            default:
                WriteBuffer(code, data1, data2, data3);
                break;
        }
    }

    string GetCodeMessage(SerialCode code)
    {
        string msg = "default";
        switch (code)
        {
            case SerialCode.TryConnect: // 연결 (0x01)
                msg = "연결 (0x01)";
                break;
            case SerialCode.Connected: // 연결 완료 (0x02)
                msg = "연결 완료 (0x02)";
                break;

            case SerialCode.ConnectOK:  // 연결 유지 (0x03)
                msg = "연결 유지 (0x03)";
                break;

            case SerialCode.InputBill:  // 지폐 입력 (0x04)
                msg = "지폐 입력 (0x04)";
                break;
            case SerialCode.InputCoin:  // 코인 입력 (0x05)
                msg = "코인 입력 (0x05)";
                break;
            case SerialCode.EnableMoney:    // 지폐, 코인 입력 가능 (0x44)
                msg = "지폐, 코인 입력 가능 (0x44)";
                break;

            case SerialCode.CardOutStart:   // 카드 배출 시작 (0x08)
                msg = "카드 배출 시작 (0x08)";
                break;
            case SerialCode.CardOuting: // 카드 배출중 (0x09)
                msg = "카드 배출중 (0x09)";
                break;

            case SerialCode.CardOutError:   // 카드 배출  에러 (0x0A)
                msg = "카드 배출 에러 (0x0A)";
                if (_ReadBuffer[1] == 0x01)
                {
                    SetCardOutState(CardOutFlag.CardTake);
                }
                else if (_ReadBuffer[1] == 0x02)
                {
                    SetCardOutState(CardOutFlag.CardJam);
                }
                break;
            case SerialCode.ReqCardState:   // 카드 상태 요구 (0x20)
                msg = "카드 상태 요구 (0x20)";
                break;

            case SerialCode.CardState:  // 카드 상태 요구 응답 (0x21)
                msg = "카드 상태 요구 응답 (0x21)";
                break;

            case SerialCode.NoCard:     // 카드 없음 (0x45)
                msg = "카드 없음 (0x45)";
                break;

            case SerialCode.CardOutOrJam:   // 카드 배출 완료 또는 걸림 (0x47)
                msg = "카드 배출 완료 또는 걸림 (0x47)";
                if (_ReadBuffer[1] == 0x01)
                    SetCardOutState(CardOutFlag.CardOutOK);
                else if (_ReadBuffer[1] == 0x02)
                    SetCardOutState(CardOutFlag.CardJam);
                break;

            case SerialCode.CardRefilled:
                msg = "CardRefilled";
                //if (_ReadBuffer[1] == 0x01)
                //	SetCardOutState(CardOutFlag.CardRefill);
                break;

            case SerialCode.ButtonInput:// 버튼 입력 (0x0B)
                msg = "버튼 입력 (0x0B)";
                break;

            case SerialCode.ButtonLEDBlink:// 버튼 램프 점멸 (0x0D)
                msg = "버튼 램프 점멸 (0x0D)";
                break;
            case SerialCode.ButtonLEDOff:// 버튼 램프 오프 (0x10)
                msg = "버튼 램프 오프 (0x10)";
                break;

            case SerialCode.ManagerMode:// 관리 버튼 (0x11)
                msg = "관리 버튼 (0x11)";
                break;

            case SerialCode.ServiceCoin:// 서비스 코인 (0x12)
                msg = "서비스 코인 (0x12)";
                break;

            case SerialCode.CounterCard:// 카운터 카드 (0x41)
                msg = "카운터 카드 (0x41)";
                break;
            case SerialCode.CounterCoin:// 카운터 코인 (0x42)
                msg = "카운터 코인 (0x42)";
                break;

            case SerialCode.ReqFirmwareVer:// 펌웨어 버전 요청 (0x15)
                msg = "펌웨어 버전 요청 (0x15)";
                break;

            case SerialCode.FirmwareVer:// 펌웨어 버전 응답 (0x16)
                msg = "펌웨어 버전 응답 (0x16)";
                break;
        }
        return msg;
    }

    void LogReadBuffer()
    {
        string msg = GetCodeMessage((SerialCode)_ReadBuffer[0]);
#if !Arcade && !UNITY_EDITOR
        if (Application.isEditor)
            Debug.Log(string.Format("Serial Read : {0:X2} {1:X2} {2:X2} {3:X2} {4:X2} : {5:}",
                _ReadBuffer[0], _ReadBuffer[1], _ReadBuffer[2], _ReadBuffer[3], _ReadBuffer[4], msg));
#endif
    }

    void LogWriteBuffer()
    {
        string msg = GetCodeMessage((SerialCode)_WriteBuffer[0]);
#if !Arcade
        if (Application.isEditor)
            Debug.LogWarning(string.Format("Serial Write1 : {0:X2} {1:X2} {2:X2} {3:X2} {4:X2} : {5:}",
                _WriteBuffer[0], _WriteBuffer[1], _WriteBuffer[2], _WriteBuffer[3], _WriteBuffer[4], msg));
#endif
    }

    void LogWriteBuffer(byte[] buffer)
    {
        string msg = GetCodeMessage((SerialCode)buffer[0]);
#if !Arcade
        if (Application.isEditor)
            Debug.LogWarning(string.Format("Serial Write2 : {0:X2} {1:X2} {2:X2} {3:X2} {4:X2} : {5:}",
                buffer[0], buffer[1], buffer[2], buffer[3], buffer[4], msg));
#endif
    }

    void CheckButtons()
    {
        //if (_IsConnected)
        //{
        //	if (!_ManagerMode && GetButtonDown(SerialButton.ServiceCredit))
        //		AddServiceCredit();

        //	if (!_ManagerMode && GetButtonDown(SerialButton.ManagerMode))
        //		Application.LoadLevel("ManagerMode");
        //}
        //else if (Global.bUseKeyboard)
        //{
        //	if (Input.GetKeyDown(Global.CoinKey))
        //		AddCoin(1);

        //	if (Input.GetKeyDown(Global.ServiceCreditKey))
        //		AddServiceCredit();

        //	if (Input.GetKeyDown(Global.ManagerModeKey))
        //		Application.LoadLevel("ManagerMode");
        //}
    }

    public bool IsConnected()
    {
        return _IsConnected;
    }

    void UpdateConnection()
    {
        if (_ReadBuffer[1] == 0x01)
            _IsConnected = true;
    }

    void UpdateButtonState()
    {
        bool[] playerButtons = null;
        if (_ReadBuffer[1] == 0x01)
        {
            playerButtons = _ThreadButtonBuffer._Player1Buttons;
        }
        else if (_ReadBuffer[1] == 0x02)
        {
            playerButtons = _ThreadButtonBuffer._Player2Buttons;
        }

        if (playerButtons != null)
        //if (playerButtons != null && _ReadBuffer[2] > 0 && _ReadBuffer[2] < 8)
        {
            int idx = _ReadBuffer[2] - 1;
            // 파4 빨2 노1
            playerButtons[0] = (_ReadBuffer[2] & 0x1) > 0; // 노
            playerButtons[1] = (_ReadBuffer[2] & 0x2) > 0; // 빨
            playerButtons[2] = (_ReadBuffer[2] & 0x4) > 0; // 파
        }
    }

    void UpdateManagementButton()
    {
        if (_ReadBuffer[1] == 0x01)
        {
            _ThreadButtonBuffer._ManagerButton = true;
            Gamemanager.instance.GoToAdmin();
        }
    }

    void UpdateServiceButton()
    {
        if (_ReadBuffer[1] == 0x01)
        {
            _ThreadButtonBuffer._CreditButton = true;
        }
    }

    void UpdateButtonBuffers()
    {
        _ButtonBuffer.SetBuffer(_ThreadButtonBuffer);
        _ThreadButtonBuffer.Clear();
    }

    void ProcessCoin()
    {
        AddCoin(1);
        //SQLiteManager.Instance.AddCoin(1);	// 코인 정산
        Write(SerialCode.CounterCoin, 0, 0, 0, 500);    // 코인 카운터 정산

        // 		else if (_Buffer[1] == 0x05)
        // 			AddCoin(5);
    }

    void ProcessBill()
    {
        int coin = 0;
        if (_ReadBuffer[1] == 0x01)
            coin = 2;
        else if (_ReadBuffer[1] == 0x05)
            coin = 10;
        else if (_ReadBuffer[1] == 0x0A)
            coin = 20;

        if (_ReadBuffer[1] > 0 && coin > 0)
        {
            AddCoin(coin);
            //SQLiteManager.Instance.AddBill(1);	// 지폐 정산
            Write(SerialCode.CounterCoin);  // 코인 카운터 정산
        }

        // 		else if (_Buffer[1] == 0x05)
        // 			AddCoin(5);
        // 		else if (_Buffer[1] == 0x0A)
        // 			AddCoin(10);
    }

    void AddCoin(int coin)
    {
        Gamemanager.instance.InsertCoin(coin);
    }

    public void AddServiceCredit()
    {
        ++_ServiceCredit;
    }

    public void SpendCredit()
    {
        if (_ServiceCredit > 0)
        {
            --_ServiceCredit;
        }
        else if (_Credit > 0)
        {
            --_Credit;
            //_Coin -= Global.gameSetup.GetCoinPerCredit();
            //Global.gameSetup.SetCoin(_Coin);
        }
    }

    public int GetCredit()
    {
        return _Credit + _ServiceCredit;
    }

    public int GetCoin()
    {
        return _Coin;
    }

    public int GetTestCoin()
    {
        return _TestCoin;
    }

    public bool GetButtonDown(SerialButton btn)
    {
        switch (btn)
        {
            case SerialButton.Player1Note1: return _ButtonBuffer._Player1Buttons[0] && !_ButtonBuffer._PrevPlayer1Buttons[0];
            case SerialButton.Player1Note2: return _ButtonBuffer._Player1Buttons[1] && !_ButtonBuffer._PrevPlayer1Buttons[1];
            case SerialButton.Player1Note3: return _ButtonBuffer._Player1Buttons[2] && !_ButtonBuffer._PrevPlayer1Buttons[2];
            case SerialButton.Player1Skill: return _ButtonBuffer._Player1Buttons[3];

            case SerialButton.Player2Note1: return _ButtonBuffer._Player2Buttons[0] && !_ButtonBuffer._PrevPlayer2Buttons[0];
            case SerialButton.Player2Note2: return _ButtonBuffer._Player2Buttons[1] && !_ButtonBuffer._PrevPlayer2Buttons[1];
            case SerialButton.Player2Note3: return _ButtonBuffer._Player2Buttons[2] && !_ButtonBuffer._PrevPlayer2Buttons[2];
            case SerialButton.Player2Skill: return _ButtonBuffer._Player2Buttons[3];

            case SerialButton.ManagerMode: return _ButtonBuffer._ManagerButton;
            case SerialButton.ServiceCredit: return _ButtonBuffer._CreditButton;
        }

        return false;
    }
    public bool GetButtonUp(SerialButton btn)
    {
        switch (btn)
        {
            case SerialButton.Player1Note1: return !_ButtonBuffer._Player1Buttons[0] && _ButtonBuffer._PrevPlayer1Buttons[0];
            case SerialButton.Player1Note2: return !_ButtonBuffer._Player1Buttons[1] && _ButtonBuffer._PrevPlayer1Buttons[1];
            case SerialButton.Player1Note3: return !_ButtonBuffer._Player1Buttons[2] && _ButtonBuffer._PrevPlayer1Buttons[2];
            case SerialButton.Player1Skill: return _ButtonBuffer._Player1Buttons[3];

            case SerialButton.Player2Note1: return !_ButtonBuffer._Player2Buttons[0] && _ButtonBuffer._PrevPlayer2Buttons[0];
            case SerialButton.Player2Note2: return !_ButtonBuffer._Player2Buttons[1] && _ButtonBuffer._PrevPlayer2Buttons[1];
            case SerialButton.Player2Note3: return !_ButtonBuffer._Player2Buttons[2] && _ButtonBuffer._PrevPlayer2Buttons[2];
            case SerialButton.Player2Skill: return _ButtonBuffer._Player2Buttons[3];

            case SerialButton.ManagerMode: return _ButtonBuffer._ManagerButton;
            case SerialButton.ServiceCredit: return _ButtonBuffer._CreditButton;
        }

        return false;
    }

    public bool GetButtonPress(SerialButton btn)
    {
        switch (btn)
        {
            case SerialButton.Player1Note1: return _ButtonBuffer._Player1Buttons[0];
            case SerialButton.Player1Note2: return _ButtonBuffer._Player1Buttons[1];
            case SerialButton.Player1Note3: return _ButtonBuffer._Player1Buttons[2];
            case SerialButton.Player1Skill: return _ButtonBuffer._Player1Buttons[3];

            case SerialButton.Player2Note1: return _ButtonBuffer._Player2Buttons[0];
            case SerialButton.Player2Note2: return _ButtonBuffer._Player2Buttons[1];
            case SerialButton.Player2Note3: return _ButtonBuffer._Player2Buttons[2];
            case SerialButton.Player2Skill: return _ButtonBuffer._Player2Buttons[3];

            case SerialButton.ManagerMode: return _ButtonBuffer._ManagerButton;
            case SerialButton.ServiceCredit: return _ButtonBuffer._CreditButton;
        }

        return false;
    }

    public void ClearCardOutState()
    {
        _CardOutFlag = 0;
    }

    public bool GetCardOutState(CardOutFlag state)
    {
        return ((_CardOutFlag & state) == state) ? true : false;
    }

    void SetCardOutState(CardOutFlag state)
    {
        if (state == CardOutFlag.CardJam || state == CardOutFlag.CardTake)
        {
            IsError = true;
            Gamemanager.instance.CardJam = true;
        }
        IsCardTaking = false;
        _CardOutFlag |= state;
    }

    void ClearCardState()
    {
        _CardStateFlag = 0;
    }

    public CardStateFlag GetCardState()
    {
        return _CardStateFlag;
    }

    void UpdateCardState()
    {
        string msg = "none";
        switch (_ReadBuffer[1])
        {
            case 0x01: // 정상
                msg = "정상";
                _CardStateFlag = CardStateFlag.OK;
                break;
            case 0x02: // 카드없음
                msg = "카드없음";
                _CardStateFlag = CardStateFlag.Empty;
                break;
            case 0x03: // 걸림
                msg = "카드걸림";
                _CardStateFlag = CardStateFlag.Jam;
                break;
            case 0x04: // 없음 & 걸림
                msg = "없음&걸림";
                _CardStateFlag = CardStateFlag.EmptyJam;
                break;

        }
#if !Arcade ||UNITY_EDITOR
        Debug.Log(msg);
#endif
    }

    public void SetManagerMode(bool value)
    {
        _ManagerMode = value;
    }

    public void SetCoinTestMode(bool value)
    {
        _CoinTestMode = value;
    }

    public void ResetCreditCoin()
    {
        _Coin = 0;
        _Credit = 0;
        _ServiceCredit = 0;
        //Global.gameSetup.SetCoin(_Coin);
    }

    public void ResetTestCoin()
    {
        _TestCoin = 0;
    }

    public bool FirmwareVersionOK()
    {
        if (_SystemIOMajorVersion == _ClientIOMajorVersion &&
            _SystemIOMinorVersion == _ClientIOMinorVersion)
            return true;
        else
            return false;
    }

    public void ButtonLED(BtnLED_Type onType, BtnLED_Player player, BtnLED_Button button, BtnLED_Blink blink = 0)
    {
        ButtonLED(onType, player, (byte)button, (byte)blink);

    }
    public void ButtonLED(BtnLED_Type onType, BtnLED_Player player, byte button, byte blink = 0)
    {
        //if (Global.demoPlayMode)
        //	return;

        SerialCode code;
        if (onType == BtnLED_Type.On)
            code = SerialCode.ButtonLEDOn;
        else if (onType == BtnLED_Type.Blink)
            code = SerialCode.ButtonLEDBlink;
        else
            code = SerialCode.ButtonLEDOff;

        Write(code, (byte)player, (byte)button, (byte)blink, 100);
    }

    public void ButtonLED_AllOff()
    {
        ButtonLED(BtnLED_Type.Off, BtnLED_Player.Player1, BtnLED_Button.All);
        ButtonLED(BtnLED_Type.Off, BtnLED_Player.Player2, BtnLED_Button.All);
    }
    public void ButtonLED_ALLOn()
    {
        ButtonLED(BtnLED_Type.On, BtnLED_Player.Player1, BtnLED_Button.All);
        ButtonLED(BtnLED_Type.On, BtnLED_Player.Player2, BtnLED_Button.All);
    }
    /// <summary>
    /// 0번 부터 시작
    /// </summary>
    /// <param name="deviceNum"></param>
    public void TakeCard(int deviceNum)
    {
        //#if !UNITY_EDITOR
        if (cardTakeDeviceCheck[deviceNum])
        {
            Write(SerialCode.CardOutStart, (byte)(deviceNum + 1));
            IsCardTaking = true;
            IsError = false;
            //SerialPortManager.Instance.GetCardOutState(CardOutFlag.CardOutOK)
            //	Write(SerialCode.CardOutStart, 0x01);
        }
        //#endif
    }
    public int GetTakeCardDevideNum()
    {
        if (cardTakeDeviceCheck[0])
            return 0;
        else if (cardTakeDeviceCheck[1])
            return 1;
        return -1;
    }

    public bool CheckTakeCard()
    {
        return cardTakeDeviceCheck[0] || cardTakeDeviceCheck[1];
    }

    public void SetSideLED(SideLED type)
    {
        int random = 0;
        switch (type)
        {
            case SideLED.Off:
                // IO 보드 수정. OFF 안됨
                Write(SerialCode.SideLED, 0x6F);

                //Write(SerialCode.SideLED, 0x77, 0x01);
                break;

            case SideLED.Title:
            case SideLED.CardScan:
            case SideLED.Stage:
            case SideLED.EnemyInfo:
            case SideLED.Minigame:
                // 기본 패턴 1-6
                random = UnityEngine.Random.Range(1, 7);
                Write(SerialCode.SideLED, 0x73, (byte)random);
                break;
            case SideLED.EnemySpawn:
            case SideLED.Catch:
                // 기본 패턴 1-6
                //random = UnityEngine.Random.Range(1, 7);
                //Write(SerialCode.SideLED, 0x73, (byte)random);

                // PINK 아래<->위(고속) IO 보드 수정 해야함
                //Write(SerialCode.SideLED, 0x70, 0x02);
                // 대신 저속 점멸로 바꿈
                Write(SerialCode.SideLED, 0x70, 0x03);
                break;
            case SideLED.CardSelect:
            case SideLED.IngameResult:
            case SideLED.CardOutRare:
                // PINK ON
                Write(SerialCode.SideLED, 0x70, 0x01);
                break;
            case SideLED.CardOutDefault:
                // WHITE ON
                Write(SerialCode.SideLED, 0x77, 0x01);
                break;
        }
    }
}

public class ButtonBuffer
{
    public bool[] _Player1Buttons = new bool[4];    // Player 1 Buttons
    public bool[] _Player2Buttons = new bool[4];    // Player 2 Buttons
    public bool[] _PrevPlayer1Buttons = new bool[4];    // Player 1 Buttons
    public bool[] _PrevPlayer2Buttons = new bool[4];    // Player 2 Buttons
    public bool _ManagerButton;
    public bool _CreditButton;

    public void Clear()
    {
        //Array.Clear(_Player1Buttons, 0, _Player1Buttons.Length);
        //Array.Clear(_Player2Buttons, 0, _Player2Buttons.Length);
        _ManagerButton = false;
        _CreditButton = false;
    }

    public void SetBuffer(ButtonBuffer buffer)
    {
        // 		Array.Copy(buffer._Player1Buttons, _Player1Buttons, _Player1Buttons.Length);
        // 		Array.Copy(buffer._Player2Buttons, _Player2Buttons, _Player2Buttons.Length);
        for (int i = 0; i < 4; ++i)
        {
            _PrevPlayer1Buttons[i] = _Player1Buttons[i];
            _PrevPlayer2Buttons[i] = _Player2Buttons[i];
        }

        for (int i = 0; i < 4; ++i)
        {
            _Player1Buttons[i] = buffer._Player1Buttons[i];
            _Player2Buttons[i] = buffer._Player2Buttons[i];
        }
        _ManagerButton = buffer._ManagerButton;
        _CreditButton = buffer._CreditButton;
    }
}

public enum SerialCode
{
    TryConnect = 0x01,  // 연결
    Connected = 0x02,   // 연결완료 (IO -> PC)
    ConnectOK = 0x03,   // 연결유지 (IO -> PC)

    InputBill = 0x04,   // 지폐입력 (IO -> PC)
    InputCoin = 0x05,   // 코인입력 (IO -> PC)
    DisableMoney = 0x43,    // 지폐, 코인 입력 중지
    EnableMoney = 0x44, // 지폐, 코인 입력 가능

    GameStart = 0x06, // 게임 시작
    GameEnd = 0x46, // 게임 끝

    CardOutStart = 0x08, // 카드 배출 시작
    CardOutComplete = 0x09, // 카드 배출 완료
    CardOutError = 0x0A, // 카드 가져감 및 에러 (IO -> PC)
    CardOuting = 0x013, // 카드 배출중 (IO -> PC)
    NoCard = 0x45,  // 카드 없음 (IO -> PC)

    CardOutOrJam = 0x47, // 카드 배출 완료 및 걸림
    CardRefilled = 0x48, // 카드 채움
    ClearCardJam = 0x0E, // 카드 걸림 해제
    ReqCardState = 0x20, // 카드 상태 요구
    CardState = 0x21, // 카드 상태 요구 응답

    ButtonInput = 0x0B, // 버튼 입력 (IO -> PC)
    ButtonLEDOn = 0x0C, // 버튼 램프 온
    ButtonLEDBlink = 0x0D,  // 버튼 램프 점멸
    ButtonLEDOff = 0x0E,    // 버튼 램프 오프

    IRLEDOn = 0x13,
    IRLEDOff = 0x14,

    ManagerMode = 0x11, // 관리 버튼 (IO -> PC)
    ServiceCoin = 0x12, // 서비스 코인 (IO -> PC)
    CounterCard = 0x41, // 카운터 카드
    CounterCoin = 0x42, // 카운터 코인

    ReqFirmwareVer = 0x15, // 펌웨어 버전 요청
    FirmwareVer = 0x16, // 펌웨어 버전 응답


    SideLED = 0x6C, // LED
}

public enum CardOutFlag
{
    CardOuting = 1 << 0,
    CardOutOK = 1 << 1,
    CardTake = 1 << 2,
    CardJam = 1 << 3,
    NoCard = 1 << 4,
    CardRefill = 1 << 5,
    None = 1 << 6,
}

public enum CardStateFlag
{
    OK = 1,
    Empty = 2,
    Jam = 3,
    EmptyJam = 4
}

public enum SerialButton
{
    Player1Note1 = 0,
    Player1Note2 = 1,
    Player1Note3 = 2,
    Player1Skill = 3,

    Player2Note1 = 4,
    Player2Note2 = 5,
    Player2Note3 = 6,
    Player2Skill = 7,

    ManagerMode = 8,    // 관리자 모드
    ServiceCredit = 9,
};

public enum BtnLED_Type
{
    On = 0x01,
    Blink = 0x02,
    Off = 0x03
}

public enum BtnLED_Player
{
    Player1 = 0x01,
    Player2 = 0x02,
    Both = 0x03
}

public enum BtnLED_Button : byte
{
    Yellow = 0x01,
    Red = 0x02,
    Blue = 0x04,
    All = Red | Blue | Yellow,
}

public enum BtnLED_Blink : byte
{
    Slow = 0x01,
    Normal = 0x02,
    Fast = 0x03,
}

public enum SideLED
{
    Off,

    // 기본패턴 1~6
    Title,
    CardScan,
    Stage,
    EnemyInfo,

    // PINK 아래<->위(고속)
    EnemySpawn,
    // 기본패턴 1~6
    Minigame,

    // PINK ON
    CardSelect,
    // PINK 아래<->위(고속)
    Catch,
    // PINK ON
    IngameResult,
    // WHITE ON
    CardOutDefault,
    // PINK ON
    CardOutRare,

}