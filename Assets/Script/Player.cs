using System;
using System.Collections;
using UnityEngine;

public class Player : PlayerController2D
{
    private static readonly int ID_Ground = Animator.StringToHash("b_isGround");
    private static readonly int ID_Jump = Animator.StringToHash("Jump");
    private static readonly int ID_Move = Animator.StringToHash("f_Move");
    private static readonly int ID_Attack = Animator.StringToHash("Attack");

    private void Start()
    {
        //사용할 키 초기화
        PlayerKeyInit(KeyCode.A, KeyCode.D, KeyCode.Mouse0, KeyCode.W);

        //착지했을 때 동작
        PlayerFallingToGround += OnPlayerFallingToGround;

        //점프 애니메이션 적용
        AnimJump = OnAnimJump;

        //떨어지는 애니메이션 적용
        AnimFall = OnAnimFall;

        //이동 애니메이션 적용
        AnimMove = OnAnimMove;

        //공격 애니메이션 적용
        AnimAttack = OnAnimAttack;

        //땅에 닿았을때, 데이터를 애니메이터에 적용
        AnimGroundCheck = OnAnimGroundCheck;
    }

    private void OnAnimGroundCheck(bool Check) =>
        animator.SetBool(ID_Ground, Check);

    private void OnAnimFall() =>
        animator.SetTrigger(ID_Jump);

    private void OnAnimMove(float Dir) =>
        animator.SetFloat(ID_Move, Dir);

    private void OnAnimJump() =>
        animator.SetTrigger(ID_Jump);

    private void OnAnimAttack()
    {
        animator.SetTrigger(ID_Attack);

        //공격시 이동 불가
        CanMove = false;
    }

    private void OnPlayerFallingToGround(object sender, EventArgs e)
    {
        //착지 모션을 수행하고 있거나 (점프 한 상태에서, 땅에 떨어지고 있는 상황이고, 땅에 닿은 상태가 아직 아니라면),
        //아래 코드 구문 실행 X
        if (isFallingToGroundMotion || !getFallingToTouchGround()) return;

        //착지 모션 상태로 전환
        isFallingToGroundMotion = true;

        //땅에 착지시 모션 실행
        StartCoroutine(IFallingToGround());
    }

    private IEnumerator IFallingToGround()
    {
        //캐릭터 이동을 얼림
        CanMove = false;

        //jump_end모션을 할때까지 대기
        yield return new WaitUntil(() => animState.IsName("jump_end"));

        //착지 애니메이션이 모두 재생된 상태가 될때까지 대기
        yield return new WaitForSeconds(animState.length);

        //이동 가능하게 함
        CanMove = true;

        //땅에 닿았을 때 모션을 해제함
        isFallingToGroundMotion = false;
    }
}