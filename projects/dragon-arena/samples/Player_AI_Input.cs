// Portfolio sample extracted from a commercial Unity project.
// Sensitive URLs/keys/tokens were redacted before publishing.
// Source: C:\Users\qlwns\Dragon-Arena\Assets\Scripts\Player\Player_AI.cs
// Lines: 128-168

    private bool AI_BuildInput(out NetworkInputData input)
    {
        input = default;
        if (!CanAIControl || NetIsDeath || !IsAlive) return false;

        // 1. 매 프레임 타겟을 먼저 찾음 (뒤에 있는 몬스터 감지)
        AI_TryAcquirePriorityTarget();
        AI_UpdateChaseTarget();

        // 2. 이동 방향 계산
        Vector3 _dirWorld = AI_GetMoveDirectionWorld();

        if (_dirWorld.sqrMagnitude < 0.0001f)
        {
            input.moveDir = Vector2.zero;
            input.IsRun = false;
            // 멈춰있을 때 타겟 조준
            if (aiIsChasing && AI_HasChaseTarget())
            {
                Vector3 _face = AI_GetChaseTargetPosition() - transform.position;
                _face.y = 0f;
                input.lookDelta = AI_ComputeLookDelta(_face.normalized);
            }
            AI_ServerUpdateCombat(ref input);
            return true;
        }

        _dirWorld.y = 0f;
        _dirWorld.Normalize();

        Vector2 _md;
        _md.x = Vector3.Dot(transform.right, _dirWorld);
        _md.y = Vector3.Dot(transform.forward, _dirWorld);
        input.moveDir = Vector2.ClampMagnitude(_md, 1f);
        input.IsRun = true; // 대상을 쫓거나 이동할 땐 항상 뛰게 설정
        input.lookDelta = AI_ComputeLookDelta(_dirWorld);

        AI_ServerUpdateCombat(ref input);
        return true;
    }
