using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

//1.0.0
//프로토타입 버전

namespace UnityEngine
{
    public enum PlayerSystem
    {
        MOVE,
        MOVE_ATTACK,
        MOVE_JUMP,
        MOVE_ATTACK_JUMP
    }

    public enum PlayerWorkType
    {
        SMOOTH,
        HARD
    }

    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CapsuleCollider2D))]
    public abstract class PlayerController2D : MonoBehaviour
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

        [SerializeField, Tooltip("플레이어가 지원할 기능")]
        private PlayerSystem playerSystem;

        [SerializeField, Tooltip("이동 방식을 부드럽게할것인지, 딱딱하게 할것인지")]
        private PlayerWorkType playerWorkType;

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

        [SerializeField, Tooltip("캐릭터의 이동 권한")]
        protected bool CanMove = true; // 캐릭터가 이동에 대한 제약 조건

        [SerializeField, Tooltip("땅에 있을때만 공격 가능")]
        protected bool CanAttackToGround = true;

        //HideInspector
        protected Rigidbody2D rig;
        protected bool isAttack;
        protected bool isFlipped;
        protected bool isJump;
        protected bool isFall;
        protected bool isGround = true;
        protected event EventHandler PlayerUpdate;
        protected event EventHandler PlayerFixedUpdate;
        protected event EventHandler PlayerFallingToGround;
        protected UnityAction AnimJump, AnimFall, AnimAttack;
        protected UnityAction<float> AnimMove;
        protected UnityAction<bool> AnimGroundCheck;

        #endregion

        private Vector2 MovementInput; //이동에 대한 값 처리
        private bool JumpInput;
        private bool AttackInput;
        private (KeyCode left, KeyCode right, KeyCode attack, KeyCode jump) Key;

        //플레이어를 조종할 키를 설정함
        protected void PlayerKeyInit(KeyCode left, KeyCode right, KeyCode attack, KeyCode jump)
        {
            Key.left = left;
            Key.right = right;
            Key.attack = attack;
            Key.jump = jump;
        }

        protected bool getFallingToTouchGround()
        {
            return isFall && !isGround && GroundCollider.IsTouchingLayers(GroundMask);
        }

        private void Awake()
        {
            //초기화
            rig = GetComponent<Rigidbody2D>();

            //콜라이더가 null이면, 자신의 콜라이더를 넣음
            if (GroundCollider == null)
                GroundCollider = GetComponent<Collider2D>();
        }

        private void Update()
        {
            //이동 할 수 없다면 아래 코드 구문 실행 X
            if (!CanMove)
                return;

            //좌우 이동, 멈춤을 수록
            float MoveHorizontal = CustomGetAxisRaw(Key.left, Key.right);
            MovementInput = new Vector2(MoveHorizontal, 0);

            if (playerSystem == PlayerSystem.MOVE_JUMP || playerSystem == PlayerSystem.MOVE_ATTACK_JUMP)
                //점프 처리
                PlayerJump();

            if (playerSystem == PlayerSystem.MOVE_ATTACK || playerSystem == PlayerSystem.MOVE_ATTACK_JUMP)
                //공격 처리
                PlayerAttack();

            PlayerUpdate?.Invoke(this, EventArgs.Empty);
        }

        private float CustomGetAxisRaw(KeyCode k1, KeyCode k2)
        {
            //리턴될 방향 변수
            float ReturnDir = 0.0f;

            //만약 일시정지 상태라면, 키 입력 처리를 하지 않는다.
            if (Time.timeScale == 0.0f) return ReturnDir;

            //방향을 왼쪽으로 처리
            if (Input.GetKey(k1))
                ReturnDir = -1.0f;

            //방향을 왼쪽으로 처리
            if (Input.GetKey(k2))
                ReturnDir = 1.0f;

            //키 누름 해제
            if (Input.GetKey(k1) && Input.GetKey(k2))
                ReturnDir = 0f;

            return ReturnDir;
        }

        private void FixedUpdate()
        {
            UpdateGround();
            UpdateHorizontalMove();
            UpdateDirection();
            UpdateFall();

            if (playerSystem == PlayerSystem.MOVE_JUMP || playerSystem == PlayerSystem.MOVE_ATTACK_JUMP)
                UpdateJump();

            if (playerSystem == PlayerSystem.MOVE_ATTACK || playerSystem == PlayerSystem.MOVE_ATTACK_JUMP)
                UpdateAttack();

            UpdateGravityScale();

            //땅에 터칭 되었을 때,


            //애니메이터 컴포넌트가 null이 아니라면 정상적으로 해당 애니메이터 상태를 갱신한다.
            animState = (AnimatorStateInfo) animator?.GetCurrentAnimatorStateInfo(0);

            PlayerFixedUpdate?.Invoke(this, EventArgs.Empty);
        }


        private void PlayerJump()
        {
            if (isGround && !isJump && Input.GetKeyDown(Key.jump))
                JumpInput = true;
        }

        private void PlayerAttack()
        {
            if (CanAttackToGround)
            {
                if (isGround && !isAttack && Input.GetKeyDown(Key.attack))
                    AttackInput = true;
            }
            else
            {
                if (!isAttack && Input.GetKeyDown(Key.attack))
                    AttackInput = true;
            }
        }

        private void UpdateGround()
        {
            //땅에 착지된 모션 실행 요건이 있다면 실행
            PlayerFallingToGround?.Invoke(this, EventArgs.Empty);

            //땅에 닿으면 true, 땅에 닿지 않은 상태라면 false를 반환
            isGround = GroundCollider.IsTouchingLayers(GroundMask);

            //땅 체크를 애니메이터에 전송 함
            AnimGroundCheck?.Invoke(isGround);
        }

        private void UpdateHorizontalMove()
        {
            //좌우 이동을 위한 벨로시티 객체 생성
            Vector2 Velocity = rig.velocity;

            switch (playerWorkType)
            {
                case PlayerWorkType.SMOOTH:
                    Velocity.x += MovementInput.x * acceleration * Time.fixedDeltaTime;
                    break;
                case PlayerWorkType.HARD:
                    Velocity.x = MovementInput.x * (acceleration * 10) * Time.fixedDeltaTime;
                    break;
            }

            //최대 이동 속도를 제한 함.
            Velocity.x = Mathf.Clamp(Velocity.x, -maxSpeed, maxSpeed);

            //적용
            rig.velocity = Velocity;

            float horizontalSpeedNormalized = 0.0f;

            switch (playerWorkType)
            {
                case PlayerWorkType.SMOOTH:
                    horizontalSpeedNormalized = Mathf.Abs(Velocity.x) / maxSpeed;
                    break;
                case PlayerWorkType.HARD:
                    horizontalSpeedNormalized = Mathf.Abs(Input.GetAxisRaw("Horizontal"));
                    break;
            }

            //좌우 이동, 멈춤에 대한 인풋 값 초기화
            MovementInput = Vector2.zero;

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

        private void UpdateJump()
        {
            //점프를 시전했고, 땅으로 떨어지는 낙하 중이라면,
            if (isJump && rig.velocity.y < 0)
                isFall = true;

            //점프의 입력이 들어왔고, 땅에 있는 경우
            if (JumpInput)
            {
                // 점프 적용 : ForceMode2D -> Impulse = 힘을 팍!, Force = 힘을 파아아아악~~!
                //Impulse는 힘을 순간적으로 적용하고, Forces는 힘을 지속적으로 준다.
                rig.AddForce(new Vector2(0, jumpForce), ForceMode2D.Impulse);

                //점프 애니메이션 적용
                AnimJump?.Invoke();

                //점프 입력을 초기화
                JumpInput = false;

                //점프 중으로 처리
                isJump = true;
            }
            //땅에 닿았을 때 데이터 처리
            else if (isJump && isFall && isGround)
            {
                // 점프 관련 변수를 초기화
                isJump = false;
                isFall = false;
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

        private void UpdateAttack()
        {
            if (AttackInput && !isAttack)
            {
                //초기화
                AttackInput = false;

                //공격 체크
                StartCoroutine(IAttack());
            }

            //공격 중이며, 이동 불가 처리를 했을 경우.
            if (!CanMove && isAttack)
                rig.velocity = Vector2.zero;

            IEnumerator IAttack()
            {
                //공격 중
                isAttack = true;

                //애니메이션 재생
                AnimAttack?.Invoke();

                //공격 애니메이션이 끝날 때까지 대기
                yield return new WaitForSeconds(animState.length);

                //공격 끝
                isAttack = false;
                CanMove = true;
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
    }
}