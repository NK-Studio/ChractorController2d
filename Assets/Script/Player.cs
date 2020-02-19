using System;
using Unity;
using UnityEngine;

public class Player : PlayerController2D
{
    private static readonly int ID_isGround = Animator.StringToHash("b_isGround");
    private static readonly int ID_Jump = Animator.StringToHash("Jump");
    private static readonly int ID_Move = Animator.StringToHash("f_Move");
    private static readonly int ID_Attack = Animator.StringToHash("Attack");

    private void Start()
    {
        //사용할 키 초기화
        PlayerKeyInit(KeyCode.A, KeyCode.D, KeyCode.Mouse0, KeyCode.W);

        //착지했을 때 동작
        PlayerFallLanding += OnPlayerFallLanding;

        //점프 애니메이션 적용
        AnimJump += OnAnimJump;

        //떨어지는 애니메이션 적용
        AnimFall += OnAnimFall;
        
        //이동 애니메이션 적용
        AnimMove += OnAnimMove;

        //공격 처리 적용
        AttackStart += OnAttackStart;
        AttackEnd += OnAttackingEnd;
    }

    private void OnAnimFall()=>
        animator.SetTrigger(ID_Jump);
    
    private void OnAnimMove(float Dir) =>
        animator.SetFloat(ID_Move, Dir);

    private void OnAnimJump() =>
        animator.SetTrigger(ID_Jump);

    private void OnAttackStart()
    {
        //공격 애니메이션 중이라면, 아래 코드 구문 실행 X
        if (animState.IsName("atk")) return;
        
        animator.SetTrigger(ID_Attack);

        //공격 중 처리
        isAttack = true;

        //이동 불가능하게 함
        CanMove = false;
    }

    private void OnAttackingEnd()
    {
        //공격 애니메이션 중이 아니라면
        if (!animState.IsName("atk")) return;

        //공격하는 애니메이션이 재생이 모두 끝났을 때
        if (!(animState.normalizedTime > 0.9f)) return;

        //공격 중임을 초기화
        isAttack = false;

        //이동 가능 처리
        CanMove = true;
    }

    private void OnPlayerFallLanding(object sender, EventArgs e)
    {
        //점프 한 상태에서, 땅에 떨어지고 있는 상황이고 땅에 닿은 상태라면
        if (isFall && !isGround && GroundCollider.IsTouchingLayers(GroundMask))
        {
            //캐릭터 이동을 얼림
            CanMove = false;

            //땅에 착지 모션 실행
            isFallEndMotion = true;
        }

        //애니메이터에 땅에 있는지 아닌지를 신호를 보냄
        animator.SetBool(ID_isGround, isGround);
        
        //땅에 착지시 모션 실행 신호가 동작하지 않았다면, 아래 코드 구문 실행 X
        if (!isFallEndMotion) return;
        
        //착지 동작을 하지 않고 있다면 아래 코드 구문 실행 X
        if (!animState.IsName("jump_end")) return;
        
        //착지 애니메이션이 모두 재생된 상태가 아니라면 아래 코드 구문 실행 X
        if (!(animState.normalizedTime > 0.99f)) return;

        //이동 가능하게 함
        CanMove = true;
        isFallEndMotion = false;
    }
}