using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

//1.0.0
//프로토타입 버전

namespace UnityEngine
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CapsuleCollider2D))]
    public abstract class ActorController2D : MonoBehaviour
    {
        private static readonly Vector3 FlipScale = new Vector3(-1, 1, 1);

        #region 파라미터

        [Header("타일 인식")]
        [SerializeField, Tooltip("땅을 인식할 레이어")]
        protected LayerMask GroundMask;

        [SerializeField, Tooltip("바닥을 체크할 콜라이더")]
        protected Collider2D GroundCollider;

        [Header("Character")]
        [SerializeField]
        protected Animator animator;

        [Tooltip("애니메이션 상태 정보를 알려줌")]
        protected AnimatorStateInfo animState;

        [Header("Movement")]
        [SerializeField, Tooltip("이동시 가해지는 가속력")]
        protected float acceleration;

        [SerializeField, Tooltip("최대 이동 속도")]
        protected float maxSpeed;

        [SerializeField, Tooltip("좌우 플립시 딜레이")]
        protected float minFlipSpeed = 0.1f;

        [SerializeField, Tooltip("점프시 힘")]
        protected float jumpForce;

        [SerializeField, Tooltip("땅에 있을 때 중력 크기")]
        protected float groundedGravityScale = 1.0f;

        [SerializeField, Tooltip("점프시 중력 크기")]
        protected float jumpGravityScale = 1.0f;

        [SerializeField, Tooltip("낙하시 중력 크기")]
        protected float fallGravityScale = 1.0f;

        #endregion

        //추적할 오브젝트
        [SerializeField, Header("추적할 오브젝트")]
        protected Transform Target;

        private Vector2 MoveDir;
        private bool JumpInput;

        protected Rigidbody2D rig;
        protected bool isTracking;
        protected bool isAttack;
        protected bool isFlipped;
        protected bool isJump;
        protected bool isMove;
        protected bool isStopped;
        protected bool isFall;
        protected bool isFallEndMotion; //점프를 하고나서 이동에 대한 처리
        protected bool isGround = true;

        protected event EventHandler ActorUpdate;
        protected event EventHandler ActorFixedUpdate;
        protected event EventHandler ActorFallLanding;
        protected UnityAction AnimJump, AnimFall;
        protected UnityAction<float> AnimMove;
        protected UnityAction AttackStart, AttackEnd;

        protected string CurrentPlayingAnimation
        {
            get
            {
                var info = animator.GetCurrentAnimatorClipInfo(0);
                foreach (var item in info)
                {
                    if (animState.IsName(item.clip.name))
                        return item.clip.name;
                }

                return null;
            }
        }
        
        [SerializeField, Header("벽인지 체크")]
        protected Transform CheckWall;

        [SerializeField,Tooltip("Ray가 벽에 닿았을 때, 점프하는 범위를 표시")]
        private bool DebugMode;
        
        protected virtual bool isShowBeginJumpMotion()
        {
            //점프하기전 모션을 수행할 것인가? 디폴트는 false
            return false;
        }

        private void Awake()
        {
            rig = GetComponent<Rigidbody2D>();

            //콜라이더가 null이면, 자신의 콜라이더를 넣음
            if (GroundCollider == null)
                GroundCollider = GetComponent<Collider2D>();
        }

        private void Update()
        {
            ActorUpdate?.Invoke(this, EventArgs.Empty);

            MoveDir = Vector2.zero;

            if (!isTracking || isStopped) return;

            //방향 구하기
            var dir = (Target.position - transform.position).normalized;

            //방향 X를 처리함
            if (dir.x > 0f)
                dir.x = 1f;
            else if (dir.x < 0f)
                dir.x = -1;

            //추적할 객체가 왼쪽에 있다면 -1, 오른쪽에 있다면 1을 계산한다. 
            MoveDir = new Vector2(dir.x, 0);

            //이동을 하고 있다면 true, 멈추었다면 false
            isMove = MoveDir.x != 0.0f;
        }

        private void FixedUpdate()
        {
            UpdateGround();
            UpdateTracking();
            UpdateDirection();
            UpdateJump();
            UpdateFall();
            UpdateGravityScale();
            UpdateWallCheck();

            UpdateTracking();

            ActorFixedUpdate?.Invoke(this, EventArgs.Empty);

            //애니메이터 컴포넌트가 null이 아니라면 정상적으로 해당 애니메이터 상태를 갱신한다.
            animState = (AnimatorStateInfo) animator?.GetCurrentAnimatorStateInfo(0);
        }

        private void UpdateGround()
        {
            //땅에 착지된 모션 실행 요건이 있다면 실행
            ActorFallLanding?.Invoke(this, EventArgs.Empty);

            //땅에 닿으면 true, 땅에 닿지 않은 상태라면 false를 반환
            isGround = GroundCollider.IsTouchingLayers(GroundMask);
        }

        private void UpdateJump()
        {
            //점프를 시전했고, 땅으로 떨어지는 낙하 중이라면,
            if (isJump && rig.velocity.y < 0)
                isFall = true;

            //점프의 입력이 들어왔고, 땅에 있는 경우
            if (JumpInput && isGround && !isJump)
            {
                //점프 입력을 초기화
                JumpInput = false;

                //점프 중으로 처리
                isJump = true;

                //점프 전 모션이 있을 경우
                if (isShowBeginJumpMotion())
                {
                    //점프 동작 후 점프를 할 수 있도록 처리
                    StartCoroutine(BeginJumpMotion());

                    //추적을 잠시 멈춤
                    isStopped = true;

                    //멈췄을 때 미끄러지는 것을 방지
                    rig.velocity = Vector2.zero;
                }
                else //점프 전 모션이 없음
                {
                    //점프 애니메이션 적용
                    AnimJump?.Invoke();
                    // 점프 적용 : ForceMode2D -> Impulse = 힘을 팍!, Force = 힘을 파아아아악~~!
                    //Impulse는 힘을 순간적으로 적용하고, Forces는 힘을 지속적으로 준다.
                    rig.AddForce(new Vector2(0, jumpForce), ForceMode2D.Impulse);
                }
            }
            //땅에 닿았을 때 데이터 처리
            else if (isJump && isFall && isGround)
            {
                // 점프 관련 변수를 초기화
                isJump = false;
                isFall = false;
            }

            IEnumerator BeginJumpMotion()
            {
                AnimJump?.Invoke();
                var currentAnim = CurrentPlayingAnimation;
                //점프 애니메이션 적용
                yield return new WaitUntil(() => !animState.IsName(currentAnim));
                yield return new WaitForSeconds(animState.length - 0.25f);
                // 점프 적용 : ForceMode2D -> Impulse = 힘을 팍!, Force = 힘을 파아아아악~~!
                //Impulse는 힘을 순간적으로 적용하고, Forces는 힘을 지속적으로 준다.
                rig.AddForce(new Vector2(transform.localScale.x * (acceleration * 0.35f), jumpForce),
                    ForceMode2D.Impulse);
            }
        }

        private void UpdateFall()
        {
            if (!isJump && !isFall && !isGround && rig.velocity.y < 0f)
            {
                isFall = true;
                AnimFall?.Invoke();
            }
            else if (isGround)
            {
                isFall = false;
            }
        }

        private void UpdateTracking()
        {
            //스톱이 true이면 아래 코드 구문 실행 X
            if (isStopped) return;

            //좌우 이동을 위한 벨로시티 객체 생성
            Vector2 Velocity = rig.velocity;

            //값 대입
            Velocity += MoveDir * acceleration * Time.fixedDeltaTime;

            //좌우 이동, 멈춤에 대한 인풋 값 초기화
            MoveDir = Vector2.zero;

            //최대 이동 속도를 제한 함.
            Velocity.x = Mathf.Clamp(Velocity.x, -maxSpeed, maxSpeed);

            //적용
            rig.velocity = Velocity;

            var horizontalSpeedNormalized = Mathf.Abs(Velocity.x) / maxSpeed;

            //애니메이션 처리
            AnimMove?.Invoke(horizontalSpeedNormalized);
        }

        private void UpdateDirection()
        {
            // 캐릭터가 플립을 세밀하게 조정
            if (rig.velocity.x > minFlipSpeed && isFlipped)
            {
                isFlipped = false;
                transform.localScale = Vector3.one;
            }
            else if (rig.velocity.x < -minFlipSpeed && !isFlipped)
            {
                isFlipped = true;
                transform.localScale = FlipScale;
            }
        }

        private void UpdateGravityScale()
        {
            // Use grounded gravity scale by default.
            var gravityScale = groundedGravityScale;

            if (!isGround)
            {
                // 만약 플레이어가 땅에 있지 않다면,점프시 그라비티 크기를 JumpGravity로 잡고,
                // 내려올때 더 강한 fallGravity로 더 쌔게 내려온다.
                gravityScale = rig.velocity.y > 0.0f ? jumpGravityScale : fallGravityScale;
            }

            rig.gravityScale = gravityScale;
        }

        private void UpdateWallCheck()
        {
            //트랙킹을 하지 않는다면, 아래 코드 구문 실행 X
            if (!isTracking) return;

            //레이를 뽕~~쏜다.
            var hit = Physics2D.Linecast(transform.position, CheckWall.position,
                LayerMask.GetMask("Ground"));

            //점프 중이 아닌 상태에서, 벽에 닿았을 때, 점프 시전
            if (!isJump && hit.collider != null)
                JumpInput = true;
        }

        protected void SetDestination(Transform pTarget)
        {
            Target = pTarget;
            isTracking = true;
        }

        protected float getDistance()
        {
            //만약 트랙킹 중이 아니라면, 0f을 반환
            if (!isTracking) return 0f;

            var distance = (Target.position - transform.position).sqrMagnitude;

            return distance;
        }

        private void OnDrawGizmos()
        {
            if(!DebugMode) return;

            //라인의 컬러를 빨간색으로 설정
            Gizmos.color = Color.red;

            //CheckWall이 null이면, 아래 코드 구문 실행 X
            if (CheckWall == null) return;

            var wallPos = CheckWall.position;
            Gizmos.DrawSphere(wallPos, 0.15f);
            Gizmos.DrawLine(transform.position, wallPos);
        }
    }
}