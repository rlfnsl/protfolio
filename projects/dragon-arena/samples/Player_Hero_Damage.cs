// Portfolio sample extracted from a commercial Unity project.
// Sensitive URLs/keys/tokens were redacted before publishing.
// Source: C:\Users\qlwns\Dragon-Arena\Assets\Scripts\Player\Player_Hero.cs
// Lines: 1693-1813

    public void DamageToHero(float _damage, UnitObject Attacker, bool _isCritical, bool ApplyHitSkill = true, bool CameraShake = true)
    {
        if (IsInvincible() || !IsAlive || Evaid())
            return;

        RPC_CancleReturn();

        #region Attacker 기록

        if (Object.HasStateAuthority)
        {
            float _now = (float)Runner.SimulationTime;

            if (Attacker is Player _attackerPlayer)
            {
                if (_attackerPlayer.PlayerInfoData != null && PlayerInfoData != null &&
                    _attackerPlayer.PlayerInfoData.Team != PlayerInfoData.Team)
                {
                    hitTimeByPlayers[_attackerPlayer.Object.Id] = _now;

                    lastHitByPlayerId = _attackerPlayer.Object.Id;
                    lastHitTime = _now;
                    hasLastHitByPlayer = true;

                    if (CanAIControl)
                    {
                        aiLastAttackerId = _attackerPlayer.Object.Id;
                        aiLastAttackerTime = _now;
                    }
                }
            }
            else if (Attacker is Monster _attackerMonster)
            {
                if (CanAIControl && _attackerMonster.Object != null)
                {
                    aiLastAttackerId = _attackerMonster.Object.Id;
                    aiLastAttackerTime = _now;
                }
            }
        }

        #endregion

        #region Block 계산

        bool _blocked = false;
        bool _parried = false;
        BlockType _blockType = BlockType.NoBlock;

        if (NetBlock && CharAnimator != null)
        {
            var _st = CharAnimator.GetCurrentAnimatorStateInfo(1);
            if (_st.IsName("Block"))
            {
                if (Attacker != null)
                {
                    Vector3 _toAttacker = Attacker.transform.position - transform.position;
                    _toAttacker.y = 0f;

                    if (_toAttacker.sqrMagnitude > 0.0001f)
                    {
                        _toAttacker.Normalize();
                        float _angle = Vector3.Angle(transform.forward, _toAttacker);

                        float _timeSinceBlock = Now - NetLastBlockStartTime;

                        if (_angle <= BlockFrontAngle)
                        {
                            _blocked = true;

                            if (_timeSinceBlock <= ParryWindow)
                                _parried = true;
                        }
                    }
                }
                else
                {
                    _blocked = true;
                }
            }
        }

        #endregion

        #region Damage 계산

        if (ApplyHitSkill)
            _damage = CalculationStatusDamage(Attacker, _damage);

        _damage -= _damage * ReduceDamageRatio;
        _damage = _damage * (100 / (100 + Defense));

        if (_blocked)
        {
            if (_parried)
            {
                _blockType = BlockType.Parried;

                _damage = 0f;

                ApplyParryAoE();

                if (Object.HasStateAuthority)
                {
                    NetBlock = false;

                    NetNextAttackTime = Now;
                    NetParriedTime = Now;

                    _pendingAttackTokens = 0;
                    _processingAttackTokens = false;

                    RPC_OnBlockSuccess();
                }
            }
            else
            {
                _blockType = BlockType.Block;
                _damage *= BlockDamageMultiplier;
            }
        }
