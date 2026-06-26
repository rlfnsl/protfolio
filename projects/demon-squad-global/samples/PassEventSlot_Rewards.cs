// Portfolio sample extracted from a commercial Unity project.
// Sensitive URLs/keys/tokens were redacted before publishing.
// Source: C:\Users\qlwns\DemonSquad_Global\Assets\2_Scripts\Pass Evnet\PassEventSlot.cs
// Lines: 6-160, 221-230

public class PassEventSlot : MonoBehaviour
{
    [SerializeField]
    private Text conditionText;

    [SerializeField]
    private Image sliderIcon;

    [SerializeField]
    private UIButton rewardFreeButton;

    [SerializeField]
    private UIButton rewardPayButton;

    [SerializeField]
    private RectTransform rewardFreeRectTranform;

    [SerializeField]
    private RectTransform rewardPayRectTranform;

    [SerializeField]
    private GameObject blockFreeObj;

    [SerializeField]
    private GameObject blockPayObj;

    private int slotType;
    private int slotIdx;

    private Color32 onSliderIconColor = new Color32(255, 213, 73, 255);
    private Color32 offSliderIconColor = new Color32(28, 28, 34, 255);

    public void InitSlot(int _type, int _idx)
    {
        slotType = _type;
        slotIdx = _idx;

        conditionText.text = GameUIManager.instance.GetCommaString(UserInfo.instance.userPassEventInfos[slotType].passConditionDelta * _idx);

        int tempRewardIdx;
        int tempRewardCnt;

        // 무료.
        tempRewardIdx = DataManager.instance.passEventData[slotType * 21 + slotIdx].freeRewardIdx;
        tempRewardCnt = DataManager.instance.passEventData[slotType * 21 + slotIdx].freeRewardCnt;

        rewardFreeButton.text.text = "";

        if (tempRewardIdx == 0)
        {
            rewardFreeButton.image.sprite = AtlasManager.instance.GetSprite(4, "DungeonTickets");
            rewardFreeButton.text.text += "x ";
        }
        else if (tempRewardIdx == 1)
        {
            rewardFreeButton.image.sprite = AtlasManager.instance.GetSprite(3, 4);
            rewardFreeButton.text.text += "x ";
        }
        else if (tempRewardIdx == 2)
            rewardFreeButton.image.sprite = AtlasManager.instance.GetSprite(3, 1);
        else if (tempRewardIdx == 3)
        {
            rewardFreeButton.image.sprite = AtlasManager.instance.GetSprite(3, 5);
            rewardFreeButton.text.text += "x ";
        }
        else if (tempRewardIdx == 4)
            rewardFreeButton.image.sprite = AtlasManager.instance.GetSprite(3, 2);
        else if (tempRewardIdx == 99)
            rewardFreeButton.image.sprite = AtlasManager.instance.GetSprite(17, 2);

        if (tempRewardIdx == 0 && rewardFreeRectTranform.sizeDelta.x != 100)
            rewardFreeRectTranform.sizeDelta = Vector2.one * 100;
        else
        {
            if (rewardFreeRectTranform.sizeDelta.x != 90)
                rewardFreeRectTranform.sizeDelta = Vector2.one * 90;
        }

        rewardFreeButton.text.text += GameUIManager.instance.GetCommaString(tempRewardCnt);


        // 유료.
        tempRewardIdx = DataManager.instance.passEventData[slotType * 21 + slotIdx].payRewardIdx;
        tempRewardCnt = DataManager.instance.passEventData[slotType * 21 + slotIdx].payRewardCnt;

        rewardPayButton.text.text = "";

        if (tempRewardIdx == 0)
        {
            rewardPayButton.image.sprite = AtlasManager.instance.GetSprite(4, "DungeonTickets");
            rewardPayButton.text.text += "x ";
        }
        else if (tempRewardIdx == 1)
        {
            rewardPayButton.image.sprite = AtlasManager.instance.GetSprite(3, 4);
            rewardPayButton.text.text += "x ";
        }
        else if (tempRewardIdx == 2)
            rewardPayButton.image.sprite = AtlasManager.instance.GetSprite(3, 1);
        else if (tempRewardIdx == 3)
        {
            rewardPayButton.image.sprite = AtlasManager.instance.GetSprite(3, 5);
            rewardPayButton.text.text += "x ";
        }
        else if (tempRewardIdx == 4)
            rewardPayButton.image.sprite = AtlasManager.instance.GetSprite(3, 2);
        else if (tempRewardIdx == 99)
            rewardPayButton.image.sprite = AtlasManager.instance.GetSprite(17, 2);

        if (tempRewardIdx == 0 && rewardPayRectTranform.sizeDelta.x != 100)
            rewardPayRectTranform.sizeDelta = Vector2.one * 100;
        else
        {
            if (rewardPayRectTranform.sizeDelta.x != 90)
                rewardPayRectTranform.sizeDelta = Vector2.one * 90;
        }

        rewardPayButton.text.text += GameUIManager.instance.GetCommaString(tempRewardCnt);

        SetButton();
    }

    public void SetButton()
    {
        if (UserInfo.instance.userPassEventInfos[slotType].freeRewardInfo[slotIdx])
            SetUI_Free(2);
        else
        {
            if (UserInfo.instance.userPassEventInfos[slotType].passConditionCnt < UserInfo.instance.userPassEventInfos[slotType].passConditionDelta * slotIdx)
                SetUI_Free(0);
            else
                SetUI_Free(1);
        }

        if (!UserInfo.instance.userPassEventInfos[slotType].isBuyPay)
        {
            SetUI_Pay(-1);
            return;
        }

        if (UserInfo.instance.userPassEventInfos[slotType].payRewardInfo[slotIdx])
            SetUI_Pay(2);
        else
        {
            if (UserInfo.instance.userPassEventInfos[slotType].passConditionCnt < UserInfo.instance.userPassEventInfos[slotType].passConditionDelta * slotIdx)
                SetUI_Pay(0);
            else
                SetUI_Pay(1);
        }
    }

    private void SetUI_Free(int _stateIdx) // 0 - 못받음, 1 - 받을 수 있음, 2 - 이미 받음.
    {
        if (_stateIdx == 0)
        {

// ...

    public void TouchSlot(int _idx)
    {
        if (_idx == 0)
        {
            GameUIManager.instance.passEventPanel.GetReward_Free(slotIdx);
        }
        else
        {
            GameUIManager.instance.passEventPanel.GetReward_Pay(slotIdx);
        }
