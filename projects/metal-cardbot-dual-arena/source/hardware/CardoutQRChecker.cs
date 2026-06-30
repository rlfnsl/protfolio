using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardoutQRChecker : MonoBehaviour
{
    public enum STATE
    {
        DEFAULT,
        CHECKING,
        ERROR,
        END,
        FAILED,
    }
    public int retry;
    public string qrdata;
    public SerialPortUnit serial;
    public int deviceNum = 0;
    public STATE state = STATE.DEFAULT;
    public int RETRY_MAX = 100;

    public void Init()
    {
        retry = 0;
        state = STATE.DEFAULT;
    }
    void Update()
    {
        if (state == STATE.CHECKING)
        {
            qrdata = serial.GetLastReadData();
            if (qrdata != "")
            {
                retry = 0;
                state = STATE.END;
            }
        }
    }

    public void StartCardoutQRCheck()
    {
        if (state == STATE.ERROR) return;

        StartCoroutine(CardoutQRCheck());
    }
    IEnumerator CardoutQRCheck()
    {
        int cnt = 0;
        state = STATE.CHECKING;
        while (true)
        {
            if (deviceNum == 0)
                serial.Write(SerialPortUnit.SerialCode.ScanStart);
            if (deviceNum == 1)
                serial.Write(SerialPortUnit.SerialCode.ScanStart);

            yield return new WaitForSeconds(1f);
            if (state != STATE.CHECKING)
                break;

            cnt++;
            if (cnt == 3)
            {
                state = STATE.FAILED;
                retry++;
                if (retry > RETRY_MAX)
                {
                    state = STATE.ERROR;
                }
#if !Arcade
                Debug.Log($"CardoutQRCheck device:{deviceNum} retry:{retry} {state}");
#endif
                break;
            }
        }
    }

    void LateUpdate()
    {
        serial.GetLastReadDataAndClear();
    }
}
