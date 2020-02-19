using System;
using Unity;
using UnityEngine;

public class Monster : ActorController2D
{
    private enum AttackType
    {
        Combo,Move
    }
    private static readonly int ID_isGround = Animator.StringToHash("b_isGround");
    private static readonly int ID_Move = Animator.StringToHash("f_Move");
    private static readonly int ID_Jump = Animator.StringToHash("Jump");
    private static readonly int ID_Fall = Animator.StringToHash("Fall");

    private AttackType attackType;
    
    private void Start()
    {
        ActorFallLanding += OnActorFallLanding;

        AnimMove += OnAnimMove;
        AnimFall += OnAnimFall;

        ActorUpdate += OnActorUpdate;
        
        //공격 방식을 랜덤으로 잡음
        //attackType = (AttackType)Random.Range(0, 2);
        attackType = AttackType.Combo;
    }

    private void OnActorFallLanding(object sender, EventArgs e)
    {
        //점프 한 상태에서, 땅에 떨어지고 있는 상황이고 땅에 닿은 상태라면
        if (isFall && !isGround && GroundCollider.IsTouchingLayers(GroundMask))
        {
            //몬스터 이동을 얼림
            isStopped = true;
            
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
        if (!(animState.normalizedTime > 0.9f)) return;

        //이동 가능하게 함
        isStopped = false;
        isFallEndMotion = false;
    }

    private void OnAnimFall()=>
        animator.SetTrigger(ID_Fall);
    
    private void OnAnimMove(float Dir) =>
        animator.SetFloat(ID_Move, Dir);

    private void OnActorUpdate(object sender, EventArgs e)
    {
        #region Test
        if (Input.GetKeyDown(KeyCode.Space))
            SetDestination(Target);
        #endregion
        
        UpdateAttack();
    }

    private void UpdateAttack()
    {
        //트랙킹 상태가 아니라면 아래 코드 구문 실행 X
        if (!isTracking) return;
        
        Debug.Log(getDistance());

        switch (attackType)
        {
            case AttackType.Combo:
                if (isMove && !isAttack && getDistance() < 22)
                {
                    isAttack = true;
                    isStopped = true;
                    rig.velocity = Vector2.zero;

                    //무브 공격
                    animator.SetTrigger("Combo_Attack");
                }else if (isAttack)
                {
                 if(animState.IsName("atk_combo"))
                     if (animState.normalizedTime > 0.9f)
                     {
                         isAttack = false;
                         isStopped = false;
                     }
                }
                break;
            case AttackType.Move:
                if(isMove && !isAttack && getDistance() < 200)
                {
                    isAttack = true;
                    isStopped = true;
                    rig.velocity = Vector2.zero;
                    
                    //콤보 공격
                    animator.SetTrigger("Move_Attack");
                }
                break;
        }
    }
}