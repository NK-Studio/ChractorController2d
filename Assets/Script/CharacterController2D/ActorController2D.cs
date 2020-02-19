using System;
using UnityEngine;
using UnityEngine.Events;

//1.0.0
//프로토타입 버전

namespace Unity
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
        protected UnityAction AnimJump,AnimFall;
        protected UnityAction<float> AnimMove;
        protected UnityAction AttackStart, AttackEnd;
        
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

            //추적할 객체가 왼쪽에 있다면 -1, 오른쪽에 있다면 1을 계산한다. 
            MoveDir = new Vector2(Mathf.RoundToInt(dir.x), 0);

            //이동을 하고 있다면 true, 멈추었다면 false
            isMove = MoveDir.x != 0.0f ? true : false;
        }

        private void FixedUpdate()
        {
            UpdateGround();
            UpdateTracking();
            UpdateDirection();
            UpdateJump();
            UpdateFall();
            UpdateGravityScale();

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
            AnimJump?.Invoke();
        }

        private void UpdateFall()
        {
            if (!isJump && !isFall && !isGround && rig.velocity.y < 0f)
            {
                isFall = true;
                AnimFall?.Invoke();
            }
            else if(isGround)
            {
                isFall = false;
            }
        }

        private void UpdateTracking()
        {
            //스톱이 true이면 아래 코드 구문 실행 X
            if(isStopped) return;
            
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
    }
}