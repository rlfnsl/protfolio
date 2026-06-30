using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class RFCard : MonoBehaviour
{
    public static RFCard instance;
    [System.Serializable]
    public class TagInfo
    {
        public string uid = string.Empty;
        public UInt32 aip_id = 0;
        public UInt32 tag_id = 0;
        [Space(10)]
        public string[] data = new string[28];
        public string[] ascildata = new string[28];

        public void Clear()
        {
            uid = "";
            aip_id = 0;
            tag_id = 0;
            data = new string[28];
            ascildata = new string[28];
        }
    }
    UIntPtr hreader;
    UIntPtr hTag;
    private bool connected = false;
    bool check = false;
    float starttime = 0;
    Coroutine cor;
    public TagInfo MyData;
    private void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
        }
        else
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
    private void Update()
    {
#if !Arcade || UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.Delete))
            DataReset();
#endif
        if (Input.inputString.Length > 0)
        {
            if (Input.inputString.IndexOf(';') >= 0 || Input.inputString.Contains(";"))
            {
                CardTaging();
            }
        }
        if (check)
        {
            starttime += Time.deltaTime;
            if (starttime > 1)
            {
                StopCardTaging();
            }
        }
    }
    public void CardTaging()
    {
        starttime = 0;
        check = true;
        MyData.Clear();
        if (cor != null)
        {
            StopCoroutine(cor);
            cor = null;
        }
        cor = StartCoroutine("CardTagingCor");
    }
    public void StopCardTaging()
    {
        check = false;
        if (cor != null)
        {
            StopCoroutine(cor);
            cor = null;
        }
    }
    IEnumerator CardTagingCor()
    {
        while (string.IsNullOrEmpty(MyData.uid))
        {
            DeviceInventoryCheck();
            yield return new WaitForSeconds(0.1f);
        }
    }
    public bool CardCheck()
    {
        return !string.IsNullOrEmpty(MyData.uid);
    }
    public string GetName()
    {
        if (MyData.data[0] == "00000000" || string.IsNullOrEmpty(MyData.data[0]))
            return null;
        return MyData.ascildata[0] + MyData.ascildata[1] + MyData.ascildata[2];
    }
    public void SetName(string _name)
    {
        if (!CardCheck()) return;
        string[] _charstring = new string[12];
        for (int i = 0; i < _name.Length; i++)
        {
            _charstring[i] = _name[i].ToString();
        }
        for (int i = 0; i < (_charstring.Length / 4) + (_charstring.Length % 4 != 0 ? 1 : 0); i++)
        {
            string frontname = "";
            for (int y = 0; y < 4; y++)
            {
                if ((i * 4) + y < _charstring.Length)
                    frontname += _charstring[(i * 4) + y];
            }
            MyData.ascildata[i] = frontname;
            MyData.data[i] = AsciiToHex(MyData.ascildata[i]);
        }
        SetCardData();
    }
    public void DataReset()
    {
        for (int i = 0; i < MyData.data.Length; i++)
        {
            MyData.data[i] = "00000000";
            MyData.ascildata[i] = "";
        }
        SetCardData();
    }
    public void PointReset()
    {
        for (int i = 18; i < MyData.data.Length - 1; i++)
        {
            MyData.data[i] = "00000000";
            MyData.ascildata[i] = "";
        }
        SetCardData();
    }
    public int GetSoloPoint()
    {
        if (!CardCheck()) return 0;
        if (MyData.data[24] == "00000000" && MyData.data[25] == "00000000" && MyData.data[26] == "00000000") return 0;
        int point = int.Parse(MyData.ascildata[24]) * 100000;
        point += int.Parse(MyData.ascildata[25]) * 10;
        point += int.Parse(MyData.ascildata[26]);
        return point;
    }
    public void SetSoloPoint(int _point)
    {
        if (!CardCheck()) return;
        string point = _point.ToString();
        string[] pointchar = new string[9];
        int count = 8;
        for (int i = point.Length - 1; i >= 0; i--)
        {
            pointchar[count--] = point[i].ToString();
        }
        for (int i = 0; i < 9; i++)
        {
            if (string.IsNullOrEmpty(pointchar[i]))
                pointchar[i] = "0";
        }
        MyData.ascildata[24] = pointchar[0] + pointchar[1] + pointchar[2] + pointchar[3];
        MyData.ascildata[25] = pointchar[4] + pointchar[5] + pointchar[6] + pointchar[7];
        MyData.ascildata[26] = pointchar[8];
        for (int i = 24; i <= 26; i++)
            MyData.data[i] = AsciiToHex(MyData.ascildata[i]);
        SetCardData();
    }
    public void SetSeason()
    {
        string point = Gamemanager.instance.Season.ToString();
        string[] pointchar = new string[4];
        int count = 3;
        for (int i = point.Length - 1; i >= 0; i--)
        {
            pointchar[count--] = point[i].ToString();
        }
        for (int i = 0; i < 4; i++)
        {
            if (string.IsNullOrEmpty(pointchar[i]))
                pointchar[i] = "0";
        }
        MyData.ascildata[27] = pointchar[0] + pointchar[1] + pointchar[2] + pointchar[3];
        MyData.data[27] = AsciiToHex(MyData.ascildata[27]);
        SetCardData();
    }
    void ConnectCard(string _uid)
    {
        int iret;
        string suid;
        int idx = 1;

        // set uid
        suid = _uid;
        byte[] uid = StringToByteArrayFastest(suid);

        //set tag type default is NXP icode sli 
        UInt32 tagType = RFIDLIB.rfidlib_def.RFID_ISO15693_PICC_ICODE_SLI_ID;

        // set address mode 
        Byte addrMode = (Byte)idx;

        // do connection
        iret = RFIDLIB.rfidlib_aip_iso15693.ISO15693_Connect(hreader, tagType, addrMode, uid, ref hTag);
        if (iret == 0)
        {
            /* 
            * if select none address mode after inventory need to reset the tag first,because the tag is stay quiet now  
            * if the tag is in ready state ,do not need to call reset
            */
            if (addrMode == 0)
            {
                iret = RFIDLIB.rfidlib_aip_iso15693.ISO15693_Reset(hreader, hTag);
                if (iret != 0)
                {
                    Debug.Log("Reset실패");
                    RFIDLIB.rfidlib_reader.RDR_TagDisconnect(hreader, hTag);
                    hTag = UIntPtr.Zero;
                    return;
                }
            }
            Debug.Log("연결성공");
            GetCardData();
        }
        else
        {
            Debug.Log("데이터가져오기 실패");
        }
    }
    void DeviceInventoryCheck()
    {
        int iret = 0;
        UIntPtr dnInvenParamList = RFIDLIB.rfidlib_reader.RDR_CreateInvenParamSpecList();
        RFIDLIB.rfidlib_aip_iso15693.ISO15693_CreateInvenParam(dnInvenParamList, (byte)0, (byte)0, (byte)0, (byte)0);
        iret = RFIDLIB.rfidlib_reader.RDR_TagInventory(hreader, RFIDLIB.rfidlib_def.AI_TYPE_NEW, 0, null, dnInvenParamList);
        if (iret != 0)
        {
            return;
        }

        UIntPtr TagDataReport = UIntPtr.Zero;
        TagDataReport = RFIDLIB.rfidlib_reader.RDR_GetTagDataReport(hreader, RFIDLIB.rfidlib_def.RFID_SEEK_FIRST); //first
        while (TagDataReport != UIntPtr.Zero)
        {
            TagInfo tag = new TagInfo();
            UInt32 aip_id = 0;
            UInt32 tag_id = 0;
            UInt32 ant_id = 0;
            Byte dsfid = 0;
            Byte[] uid = new Byte[16];
            string strUid = "";

            iret = RFIDLIB.rfidlib_aip_iso15693.ISO15693_ParseTagDataReport(TagDataReport, ref aip_id, ref tag_id, ref ant_id, ref dsfid, uid);
            if (iret == 0) //ISO15693��ǩ
            {
                strUid = BitConverter.ToString(uid, 0, 8).Replace("-", string.Empty);
                tag.uid = strUid;
                tag.aip_id = aip_id;
                tag.tag_id = tag_id;
                MyData = tag;
                if (cor != null)
                {
                    StopCoroutine(cor);
                    cor = null;
                }
                Debug.Log($"{strUid} 카드 인식");
                ConnectCard(strUid);
            }

            TagDataReport = RFIDLIB.rfidlib_reader.RDR_GetTagDataReport(hreader, RFIDLIB.rfidlib_def.RFID_SEEK_NEXT); //next
        }
        //Debug.Log("DeviceInventoryCheck End");
        //if(!trySuccess)
        //    LoginStart_Auto();
    }
    private void GetCardData()
    {
        int iret;
        int idx;
        UInt32 blockAddr;
        UInt32 blockToRead;
        UInt32 blocksRead = 0;
        idx = 0;
        blockAddr = (UInt32)idx;
        idx = 27;
        blockToRead = (UInt32)(idx + 1);
        UInt32 nSize;
        Byte[] BlockBuffer = new Byte[8 * 27];

        nSize = (UInt32)BlockBuffer.GetLength(0);
        UInt32 bytesRead = 0;
        iret = RFIDLIB.rfidlib_aip_iso15693.ISO15693_ReadMultiBlocks(hreader, hTag, 0, blockAddr, blockToRead, ref blocksRead, BlockBuffer, nSize, ref bytesRead);
        if (iret == 0)
        {
            //blocksRead: blocks read 
            string _result = BitConverter.ToString(BlockBuffer, 0, (int)bytesRead).Replace("-", string.Empty);
            for (int i = 0; i < 28; i++)
            {
                MyData.data[i] = _result.Substring(i * 8, 8);
                MyData.ascildata[i] = HexToAscii(_result.Substring(i * 8, 8));
            }
            if (MyData.data[27] == "00000000")
            {
                SetSeason();
            }
            else
            {
                if (int.Parse(MyData.ascildata[27]) != Gamemanager.instance.Season)
                {
                    PointReset();
                    SetSeason();
                }
            }
            Debug.Log("Success Get Data!");
        }
        else
        {
            Debug.Log("Fail Get Data...");
        }

    }
    private void SetCardData()
    {
        int iret;
        int idx;
        UInt32 blkAddr;
        UInt32 numOfBlks;
        idx = 0;
        blkAddr = (UInt32)idx;
        idx = 27;
        numOfBlks = (UInt32)(idx + 1);
        string _data = "";
        for (int i = 0; i < 28; i++)
            _data += MyData.data[i];
        byte[] newBlksData = StringToByteArrayFastest(_data);

        iret = RFIDLIB.rfidlib_aip_iso15693.ISO15693_WriteMultipleBlocks(hreader, hTag, blkAddr, numOfBlks, newBlksData, (uint)newBlksData.Length);
        if (iret == 0)
        {
            Debug.Log("성공");
        }
        else
        {
            Debug.Log("실패");
        }
    }
    #region 계산
    public static string HexToAscii(string hex)
    {
        if (hex.Length % 2 != 0)
        {
            throw new ArgumentException("Hex 문자열의 길이는 짝수여야 합니다.");
        }

        StringBuilder ascii = new StringBuilder(hex.Length / 2);

        for (int i = 0; i < hex.Length; i += 2)
        {
            string hexByte = hex.Substring(i, 2);
            byte asciiByte = byte.Parse(hexByte, System.Globalization.NumberStyles.HexNumber);
            ascii.Append((char)asciiByte);
        }

        return ascii.ToString();
    }
    public static string AsciiToHex(string ascii)
    {
        if (string.IsNullOrEmpty(ascii))
            return "00000000";

        StringBuilder hex = new StringBuilder(ascii.Length * 2);

        foreach (char c in ascii)
        {
            string hexValue = ((int)c).ToString("X2"); // 문자를 16진수로 변환 (두 자리)
            hex.Append(hexValue);
        }
        while (hex.Length < 8)
        {
            hex.Append("00");
        }

        return hex.ToString();
    }
    public static byte[] StringToByteArrayFastest(string hex)
    {
        if (hex.Length % 2 == 1)
            throw new Exception("The binary key cannot have an odd number of digits");

        int len = hex.Length >> 1;
        byte[] arr = new byte[len];

        for (int i = 0; i < len; ++i)
        {
            arr[i] = (byte)((GetHexVal(hex[i << 1]) << 4) + (GetHexVal(hex[(i << 1) + 1])));
        }

        return arr;
    }
    public static int GetHexVal(char hex)
    {
        int val = (int)hex;
        return val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
    }
    #endregion
    #region 카드리더오픈
    void OnEnable()
    {
        DeviceConnect();
    }
    private void OnDisable()
    {
        DeviceDisconnect();
    }
    /// <summary>
    /// 드라이버 목록을 확인합니다.
    /// 컴퓨터 부팅 후 최소 한번은 실행해야 하므로 항상 실행시킬것
    /// </summary>
    void DeviceDriverList()
    {
#if UNITY_EDITOR
        //Debug.LogWarning($"{Application.dataPath}/Plugins/Drivers");
        RFIDLIB.rfidlib_reader.RDR_LoadReaderDrivers($"{Application.dataPath}/Plugins/Drivers");
#else
        //Debug.LogWarning($"{Application.dataPath}/Plugins/x86_64");
        RFIDLIB.rfidlib_reader.RDR_LoadReaderDrivers($"{Application.dataPath}/Plugins/x86_64");
#endif
        UInt32 nReaderCnt = RFIDLIB.rfidlib_reader.RDR_GetLoadedReaderDriverCount();
        for (UInt32 j = 0; j < nReaderCnt; j++)
        {
            StringBuilder nameBuffer = new StringBuilder();
            nameBuffer.Append('\0', 128);
            UInt32 nameLen = (UInt32)nameBuffer.Length;
            RFIDLIB.rfidlib_reader.RDR_GetLoadedReaderDriverOpt(j, RFIDLIB.rfidlib_def.LOADED_RDRDVR_OPT_NAME, nameBuffer, ref nameLen);
            //Debug.LogWarning(nameBuffer.ToString().Replace("\0", ""));
        }
    }
    /// <summary>
    /// RFID 리더기에 연결합니다.
    /// </summary>
    void DeviceConnect(bool reconnect = true)
    {
        if (connected) return;
        DeviceDriverList();//컴퓨터 부팅 후 드라이버 활성화를 않하면 연결 안됨

        string readerDriverName = "RL8000";
        string connstr = $"{RFIDLIB.rfidlib_def.CONNSTR_NAME_RDTYPE}={readerDriverName};" +
            $"{RFIDLIB.rfidlib_def.CONNSTR_NAME_COMMTYPE}={RFIDLIB.rfidlib_def.CONNSTR_NAME_COMMTYPE_USB};" +
            $"{RFIDLIB.rfidlib_def.CONNSTR_NAME_HIDADDRMODE}=0;" +
            $"{RFIDLIB.rfidlib_def.CONNSTR_NAME_HIDSERNUM}=";
        int iret = RFIDLIB.rfidlib_reader.RDR_Open(connstr, ref hreader);
        if (iret != 0)
        {
            Debug.Log("RFID failed! iret:" + iret);
            iret = RFIDLIB.rfidlib_reader.RDR_Close(hreader);
            Debug.Log("failed : " + iret);
            iret = RFIDLIB.rfidlib_reader.RDR_TagDisconnect(hreader, hTag);
            Debug.Log("failed : " + iret);
            iret = RFIDLIB.rfidlib_reader.RDR_CloseRFTransmitter(hreader);
            Debug.Log("failed : " + iret);
            //RFIDLIB.rfidlib_reader.RDR_GetLoadedReaderDriverOpt(j, RFIDLIB.rfidlib_def.LOADED_RDRDVR_OPT_NAME, nameBuffer, ref nameLen);
            iret = RFIDLIB.rfidlib_reader.RDR_ResetCommuImmeTimeout(hreader);
            Debug.Log("failed : " + iret);
            iret = RFIDLIB.rfidlib_reader.RDR_GetReaderLastReturnError(hreader);
            Debug.Log("failed : " + iret);
            if (reconnect)
            {
                DeviceConnect(false);
            }
        }
        else
        {
            Debug.Log("RFID sucess!");
            connected = true;
        }
    }
    void DeviceDisconnect()
    {
        if (hreader == UIntPtr.Zero)
            return;

        if (hTag != UIntPtr.Zero)
            CardDisconnect();

        RFIDLIB.rfidlib_reader.RDR_Close(hreader);
#if !Arcade||UNITY_EDITOR
        Debug.LogWarning("RFID close");
#endif
    }
    void CardDisconnect()
    {
        if (hTag == UIntPtr.Zero)
            return;

        hTag = UIntPtr.Zero;
        int iret = RFIDLIB.rfidlib_reader.RDR_TagDisconnect(hreader, hTag);
    }
    #endregion
}
