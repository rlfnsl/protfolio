// Portfolio sample extracted from a commercial Unity project.
// Sensitive URLs/keys/tokens were redacted before publishing.
// Source: C:\Users\qlwns\ProjectDA\ProjectDA\Assets\Script\Serial\SerialPortManager.cs
// Lines: 69-178, 261-307, 426-503

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

// ...

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


// ...

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
