// Portfolio sample extracted from a commercial Unity project.
// Sensitive URLs/keys/tokens were redacted before publishing.
// Source: C:\Users\qlwns\ProjectDA\ProjectDA\Assets\Script\RFID\RFCard.cs
// Lines: 240-282

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
            if (iret == 0) // ISO15693 tag parsed
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
