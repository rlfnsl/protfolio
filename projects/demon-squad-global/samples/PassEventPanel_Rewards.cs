// Portfolio sample extracted from a commercial Unity project.
// Sensitive URLs/keys/tokens were redacted before publishing.
// Source: C:\Users\qlwns\DemonSquad_Global\Assets\2_Scripts\Pass Evnet\PassEventPanel.cs
// Lines: 102-161, 166-290, 295-446, 451-591

    public void TouchCategory(int _idx)
    {
        selectedType = _idx;

        for (int i = 0; i < categoryUIButtons.Length; i++)
        {
            if (i == selectedType)
            {
                categoryUIButtons[i].image.color = Color.white;
                categoryEffects[i].SetActive(true);
                passBackObjs[i].SetActive(true);
            }
            else
            {
                categoryUIButtons[i].image.color = categoryColor;
                categoryEffects[i].SetActive(false);
                passBackObjs[i].SetActive(false);
            }
        }

        // 슬롯 초기화.
        for (int i = 0; i < rewardFreeSlots.Length; i++)
            rewardFreeSlots[i].InitSlot(_idx, i);

        // 패스 이미지 초기화.
        for (int i = 0; i < passImages.Length; i++)
            passImages[i].sprite = passSprites[selectedType];

        // 슬라이더 초기화.
        mainSlider.maxValue = 19 * UserInfo.instance.userPassEventInfos[selectedType].passConditionDelta;

        for (int i = 0; i < finalRewardSlider.Length; i++)
            finalRewardSlider[i].maxValue = UserInfo.instance.userPassEventInfos[selectedType].passConditionDelta;

        tempTime = UserInfo.instance.userPassEventInfos[selectedType].endDate;
        //endTimeText.text = string.Format(LocalizationService.Instance.GetTextByKey("Date_Until_Day_Month"), tempTime.Month, tempTime.Day);
        endTimeText.text = string.Format(LocalizationService.Instance.GetTextByKey("Text_End_Date_Detail"), tempTime.Month, tempTime.Day, 12, "00");

        if (selectedType == 0)
        {
            missionText.text = " " + LocalizationService.Instance.GetTextByKey("Text_Stage_Clear");
            passDetailText.text = LocalizationService.Instance.GetTextByKey("Stage_Pass_Ment");
        }
        else if (selectedType == 1)
        {
            missionText.text = " " +  LocalizationService.Instance.GetTextByKey("Shop_Weapon_Draw");
            passDetailText.text = LocalizationService.Instance.GetTextByKey("Weapon_Pass_Ment");
        }
        else if (selectedType == 2)
        {
            missionText.text = " " + LocalizationService.Instance.GetTextByKey("Shop_Armor_Draw");
            passDetailText.text = LocalizationService.Instance.GetTextByKey("Armor_Pass_Ment");
        }

        conditionText.text = "(" + GameUIManager.instance.GetCommaString(UserInfo.instance.userPassEventInfos[selectedType].passConditionCnt) + " / " + GameUIManager.instance.GetCommaString(20 * UserInfo.instance.userPassEventInfos[selectedType].passConditionDelta) + ")";

        UpdateSlider();

        SetBuyPassButton();
        SetGetRewardAllButton();

// ...

    public void GetReward_Free(int _idx)
    {
        if (UserInfo.instance.userPassEventInfos[selectedType].passConditionCnt < UserInfo.instance.userPassEventInfos[selectedType].passConditionDelta * _idx)
            return;

        if (UserInfo.instance.userPassEventInfos[selectedType].freeRewardInfo[_idx])
            return;

        ServerFunction.Instance.Event_Pass_Reward(UserInfo.instance.userId, selectedType, _idx, false, (string callback) =>
        {
            if (callback.Equals("OK"))
            {
                UserInfo.instance.userPassEventInfos[selectedType].freeRewardInfo[_idx] = true;

                int tempIdx = DataManager.instance.passEventData[selectedType * 21 + _idx].freeRewardIdx;
                int tempCnt = DataManager.instance.passEventData[selectedType * 21 + _idx].freeRewardCnt;

                if (tempIdx == 0)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        UserInfo.instance.userDungeonTicket[i] += tempCnt;
                        GameUIManager.instance.rewardPanel.SetRewardSlot_Goods(1, 9 + i, tempCnt);
                    }
                }
                else if (tempIdx == 1)
                {
                    UserInfo.instance.userWeaponDrawTicket += tempCnt;
                    GameUIManager.instance.rewardPanel.SetRewardSlot_Goods(1, 4, tempCnt);
                }
                else if (tempIdx == 2)
                {
                    UserInfo.instance.userWeaponScroll += tempCnt;
                    GameUIManager.instance.rewardPanel.SetRewardSlot_Goods(1, 1, tempCnt);
                }
                else if (tempIdx == 3)
                {
                    UserInfo.instance.userArmorDrawTicktet += tempCnt;
                    GameUIManager.instance.rewardPanel.SetRewardSlot_Goods(1, 5, tempCnt);
                }
                else if (tempIdx == 4)
                {
                    UserInfo.instance.userArmorScroll += tempCnt;
                    GameUIManager.instance.rewardPanel.SetRewardSlot_Goods(1, 2, tempCnt);
                }
                else if (tempIdx == 99)
                {
                    UserInfo.instance.userRuby += tempCnt;
                    GoodsUIManager.instance.UpdateRubyText(UserInfo.instance.userRuby);
                    GameUIManager.instance.rewardPanel.SetRewardSlot_Goods(0, 2, tempCnt);
                }

                GameUIManager.instance.rewardPanel.OpenPanel();

                // 슬롯 갱신.
                rewardFreeSlots[_idx].SetButton();

                CanOnRedLight();
                SetGetRewardAllButton();
            }
        });
    }

    public void GetReward_Pay(int _idx)
    {
        if (!UserInfo.instance.userPassEventInfos[selectedType].isBuyPay)
            return;

        if (UserInfo.instance.userPassEventInfos[selectedType].passConditionCnt < UserInfo.instance.userPassEventInfos[selectedType].passConditionDelta * _idx)
            return;

        if (UserInfo.instance.userPassEventInfos[selectedType].payRewardInfo[_idx])
            return;

        ServerFunction.Instance.Event_Pass_Reward(UserInfo.instance.userId, selectedType, _idx, true, (string callback) =>
        {
            if (callback.Equals("OK"))
            {
                UserInfo.instance.userPassEventInfos[selectedType].payRewardInfo[_idx] = true;

                int tempIdx = DataManager.instance.passEventData[selectedType * 21 + _idx].payRewardIdx;
                int tempCnt = DataManager.instance.passEventData[selectedType * 21 + _idx].payRewardCnt;

                if (tempIdx == 0)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        UserInfo.instance.userDungeonTicket[i] += tempCnt;
                        GameUIManager.instance.rewardPanel.SetRewardSlot_Goods(1, 9 + i, tempCnt);
                    }
                }
                else if (tempIdx == 1)
                {
                    UserInfo.instance.userWeaponDrawTicket += tempCnt;
                    GameUIManager.instance.rewardPanel.SetRewardSlot_Goods(1, 4, tempCnt);
                }
                else if (tempIdx == 2)
                {
                    UserInfo.instance.userWeaponScroll += tempCnt;
                    GameUIManager.instance.rewardPanel.SetRewardSlot_Goods(1, 1, tempCnt);
                }
                else if (tempIdx == 3)
                {
                    UserInfo.instance.userArmorDrawTicktet += tempCnt;
                    GameUIManager.instance.rewardPanel.SetRewardSlot_Goods(1, 5, tempCnt);
                }
                else if (tempIdx == 4)
                {
                    UserInfo.instance.userArmorScroll += tempCnt;
                    GameUIManager.instance.rewardPanel.SetRewardSlot_Goods(1, 2, tempCnt);
                }
                else if (tempIdx == 99)
                {
                    UserInfo.instance.userRuby += tempCnt;
                    GoodsUIManager.instance.UpdateRubyText(UserInfo.instance.userRuby);
                    GameUIManager.instance.rewardPanel.SetRewardSlot_Goods(0, 2, tempCnt);
                }

                GameUIManager.instance.rewardPanel.OpenPanel();

                // 슬롯 갱신.
                rewardFreeSlots[_idx].SetButton();

                CanOnRedLight();
                SetGetRewardAllButton();

// ...

    public void GetReward_All()
    {
        if (!CanGetReward(selectedType))
            return;

        ServerFunction.Instance.Event_Pass_Reward_All(UserInfo.instance.userId, selectedType, (string callback) =>
        {
            if (callback.Equals("OK"))
            {
                int[] tempCnts = new int[6] { 0, 0, 0, 0, 0, 0 };

                int tempIdx;
                int tempCnt;

                for (int i = 0; i < rewardFreeSlots.Length; i++)
                {
                    if (!UserInfo.instance.userPassEventInfos[selectedType].freeRewardInfo[i] && UserInfo.instance.userPassEventInfos[selectedType].passConditionCnt >= UserInfo.instance.userPassEventInfos[selectedType].passConditionDelta * i)
                    {
                        UserInfo.instance.userPassEventInfos[selectedType].freeRewardInfo[i] = true;

                        tempIdx = DataManager.instance.passEventData[selectedType * 21 + i].freeRewardIdx;
                        tempCnt = DataManager.instance.passEventData[selectedType * 21 + i].freeRewardCnt;

                        if (tempIdx == 0)
                        {
                            for (int j = 0; j < 4; j++)
                            {
                                UserInfo.instance.userDungeonTicket[j] += tempCnt;
                            }

                            tempCnts[0] += tempCnt;
                        }
                        else if (tempIdx == 1)
                        {
                            UserInfo.instance.userWeaponDrawTicket += tempCnt;

                            tempCnts[1] += tempCnt;
                        }
                        else if (tempIdx == 2)
                        {
                            UserInfo.instance.userWeaponScroll += tempCnt;

                            tempCnts[2] += tempCnt;
                        }
                        else if (tempIdx == 3)
                        {
                            UserInfo.instance.userArmorDrawTicktet += tempCnt;

                            tempCnts[3] += tempCnt;
                        }
                        else if (tempIdx == 4)
                        {
                            UserInfo.instance.userArmorScroll += tempCnt;

                            tempCnts[4] += tempCnt;
                        }
                        else if (tempIdx == 99)
                        {
                            UserInfo.instance.userRuby += tempCnt;

                            tempCnts[5] += tempCnt;
                        }

                        rewardFreeSlots[i].SetButton();
                    }
                }

                if (UserInfo.instance.userPassEventInfos[selectedType].isBuyPay)
                {
                    for (int i = 0; i < rewardFreeSlots.Length; i++)
                    {
                        if (!UserInfo.instance.userPassEventInfos[selectedType].payRewardInfo[i] && UserInfo.instance.userPassEventInfos[selectedType].passConditionCnt >= UserInfo.instance.userPassEventInfos[selectedType].passConditionDelta * i)
                        {
                            UserInfo.instance.userPassEventInfos[selectedType].payRewardInfo[i] = true;

                            tempIdx = DataManager.instance.passEventData[selectedType * 21 + i].payRewardIdx;
                            tempCnt = DataManager.instance.passEventData[selectedType * 21 + i].payRewardCnt;

                            if (tempIdx == 0)
                            {
                                for (int j = 0; j < 4; j++)
                                {
                                    UserInfo.instance.userDungeonTicket[j] += tempCnt;
                                }

                                tempCnts[0] += tempCnt;
                            }
                            else if (tempIdx == 1)
                            {
                                UserInfo.instance.userWeaponDrawTicket += tempCnt;

                                tempCnts[1] += tempCnt;
                            }
                            else if (tempIdx == 2)
                            {
                                UserInfo.instance.userWeaponScroll += tempCnt;

                                tempCnts[2] += tempCnt;
                            }
                            else if (tempIdx == 3)
                            {
                                UserInfo.instance.userArmorDrawTicktet += tempCnt;

                                tempCnts[3] += tempCnt;
                            }
                            else if (tempIdx == 4)
                            {
                                UserInfo.instance.userArmorScroll += tempCnt;

                                tempCnts[4] += tempCnt;
                            }
                            else if (tempIdx == 99)
                            {
                                UserInfo.instance.userRuby += tempCnt;

                                tempCnts[5] += tempCnt;
                            }

                            rewardFreeSlots[i].SetButton();
                        }
                    }
                }

                GoodsUIManager.instance.UpdateRubyText(UserInfo.instance.userRuby);

                if (tempCnts[0] > 0)
                {
                    GameUIManager.instance.rewardPanel.SetRewardSlot_Goods(1, 9, tempCnts[0]);
                    GameUIManager.instance.rewardPanel.SetRewardSlot_Goods(1, 10, tempCnts[0]);
                    GameUIManager.instance.rewardPanel.SetRewardSlot_Goods(1, 11, tempCnts[0]);
                    GameUIManager.instance.rewardPanel.SetRewardSlot_Goods(1, 12, tempCnts[0]);
                }

                if (tempCnts[1] > 0)
                    GameUIManager.instance.rewardPanel.SetRewardSlot_Goods(1, 4, tempCnts[1]);

                if (tempCnts[2] > 0)
                    GameUIManager.instance.rewardPanel.SetRewardSlot_Goods(1, 1, tempCnts[2]);

                if (tempCnts[3] > 0)
                    GameUIManager.instance.rewardPanel.SetRewardSlot_Goods(1, 5, tempCnts[3]);

                if (tempCnts[4] > 0)
                    GameUIManager.instance.rewardPanel.SetRewardSlot_Goods(1, 2, tempCnts[4]);

                if (tempCnts[5] > 0)
                    GameUIManager.instance.rewardPanel.SetRewardSlot_Goods(0, 2, tempCnts[5]);

                GameUIManager.instance.rewardPanel.OpenPanel();

                CanOnRedLight();
                SetGetRewardAllButton();

// ...

    private bool CanGetReward(int _passType)
    {
        for (int i = 0; i < rewardFreeSlots.Length; i++)
        {
            if (!UserInfo.instance.userPassEventInfos[_passType].freeRewardInfo[i] && UserInfo.instance.userPassEventInfos[_passType].passConditionCnt >= UserInfo.instance.userPassEventInfos[_passType].passConditionDelta * i)
            {
                return true;
            }
        }

        if (!UserInfo.instance.userPassEventInfos[_passType].isBuyPay)
            return false;

        for (int i = 0; i < rewardFreeSlots.Length; i++)
        {
            if (!UserInfo.instance.userPassEventInfos[_passType].payRewardInfo[i] && UserInfo.instance.userPassEventInfos[_passType].passConditionCnt >= UserInfo.instance.userPassEventInfos[_passType].passConditionDelta * i)
            {
                return true;
            }
        }

        return false;
    }

    private void CanOnRedLight()
    {
        if (ServerFunction.Instance.serverFlags_2[18])
            return;

        foreach (var info in UserInfo.instance.userPassEventInfos)
        {
            if (ServerFunction.Instance.serverDateTime < info.Value.endDate)
                categoryUIButtons[info.Value.passType].iconImage.gameObject.SetActive(CanGetReward(info.Value.passType));
        }

        GameUIManager.instance.CanOnPassEventRedLight();
    }

    private void SetBuyPassButton()
    {
        if (UserInfo.instance.userPassEventInfos[selectedType].isBuyPay)
        {
            buyPassUIButton.image.color = Color.gray;
            buyPassUIButton.iconImage.color = Color.gray;

            buyPassUIButton.button.interactable = false;
            buyPassUIButton.text.text = LocalizationService.Instance.GetTextByKey("Button_Buy_Completed");

            if (selectedType == 0)
            {
                for (int i = 0; i < passPriceTexts.Length; i++)
                    passPriceTexts[i].text = "";
            }
            else if (selectedType == 1)
            {
                for (int i = 0; i < passPriceTexts.Length; i++)
                    passPriceTexts[i].text = "";
            }
            else if (selectedType == 2)
            {
                for (int i = 0; i < passPriceTexts.Length; i++)
                    passPriceTexts[i].text = "";
            }

            blockPayPassObj.SetActive(false);
        }
        else
        {
            buyPassUIButton.image.color = Color.white;
            buyPassUIButton.iconImage.color = Color.white;

            buyPassUIButton.button.interactable = true;
            buyPassUIButton.text.text = LocalizationService.Instance.GetTextByKey("Text_Buy");

            if (selectedType == 0)
            {
                for (int i = 0; i < passPriceTexts.Length; i++)
                    passPriceTexts[i].text = InappManager.GetInstance().Get_Price(250, 101);
            }
            else if (selectedType == 1)
            {
                for (int i = 0; i < passPriceTexts.Length; i++)
                    passPriceTexts[i].text = InappManager.GetInstance().Get_Price(250, 102);
            }
            else if (selectedType == 2)
            {
                for (int i = 0; i < passPriceTexts.Length; i++)
                    passPriceTexts[i].text = InappManager.GetInstance().Get_Price(250, 103);
            }

            blockPayPassObj.SetActive(true);
        }
    }

    private void SetGetRewardAllButton()
    {
        if (CanGetReward(selectedType))
        {
            getRewardAllButton.button.interactable = true;

            getRewardAllButton.iconImage.gameObject.SetActive(true);
        }
        else
        {
            getRewardAllButton.button.interactable = false;

            getRewardAllButton.iconImage.gameObject.SetActive(false);
        }
    }

    private void UpdateSlider()
    {
        mainSlider.value = UserInfo.instance.userPassEventInfos[selectedType].passConditionCnt;

        for (int i = 0; i < finalRewardSlider.Length; i++)
            finalRewardSlider[i].value = UserInfo.instance.userPassEventInfos[selectedType].passConditionCnt - UserInfo.instance.userPassEventInfos[selectedType].passConditionDelta * 19;
    }

    public void TouchBuyPassButton()
    {
        if (UserInfo.instance.userPassEventInfos[selectedType].isBuyPay)
            return;

        if (selectedType == 0)
            InappManager.GetInstance().Buy_Inapp_Product(250, 101, false);
        else if (selectedType == 1)
            InappManager.GetInstance().Buy_Inapp_Product(250, 102, false);
        else if (selectedType == 2)
            InappManager.GetInstance().Buy_Inapp_Product(250, 103, false);
    }

    public void EndBuyPass()
    {
        if (selectedType == 0)
            NoticeManager.instance.OpenPanel(LocalizationService.Instance.GetTextByKey("Notice_Stage_Pass_Buy"));
        else if (selectedType == 1)
            NoticeManager.instance.OpenPanel(LocalizationService.Instance.GetTextByKey("Notice_Weaopn_Pass_Buy"));
        else if (selectedType == 2)
            NoticeManager.instance.OpenPanel(LocalizationService.Instance.GetTextByKey("Notice_Armor_Pass_Buy"));

        TouchCategory(selectedType);
