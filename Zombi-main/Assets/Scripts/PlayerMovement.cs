using UnityEngine;

// 플레이어 캐릭터를 사용자 입력에 따라 움직이는 스크립트
public class PlayerMovement : MonoBehaviour {
    public float moveSpeed = 0.1f; // 앞뒤 움직임의 속도
    public float rotateSpeed = 180f; // 좌우 회전 속도

    private Animator playerAnimator; // 플레이어 캐릭터의 애니메이터
    private PlayerInput playerInput; // 플레이어 입력을 알려주는 컴포넌트
    private Rigidbody playerRigidbody; // 플레이어 캐릭터의 리지드바디

    private void Start() {
        // 사용할 컴포넌트들의 참조를 가져오기
        playerInput = GetComponent<PlayerInput>();
        playerRigidbody = GetComponent<Rigidbody>();
        playerAnimator = GetComponent<Animator>();
    }

    // FixedUpdate는 물리 갱신 주기에 맞춰 실행됨
    private void FixedUpdate() {
        // 회전 실행
        Rotate();
        // 움직임 실행
        Move();

        // 입력값에 따라 애니메이터의 Move 파라미터 값을 변경
        playerAnimator.SetFloat("Move", playerInput.move);
    }

    // 입력값에 따라 캐릭터를 앞뒤로 움직임
    // private void Move() {
    //     // 상대적으로 이동할 거리 계산
    //     Vector3 moveDistance =
    //         playerInput.move * transform.forward * moveSpeed * Time.deltaTime;
    //     // 리지드바디를 통해 게임 오브젝트 위치 변경
    //     playerRigidbody.MovePosition(playerRigidbody.position + moveDistance);
    // }

    private void Move() {
    // 1. 캐릭터의 방향과 무관하게 '세상의 절대적인 방향'을 기준으로 계산
    // Vector3.forward = (0, 0, 1) -> 화면 위쪽 방향
    Vector3 moveForward = new Vector3(0.5f,0,0.5f) * playerInput.move * moveSpeed * Time.deltaTime;

    // Vector3.right = (1, 0, 0) -> 화면 오른쪽 방향
    Vector3 moveSide = new Vector3(0.5f,0,-0.5f) * playerInput.rotate * moveSpeed * Time.deltaTime;

    // 2. 두 벡터를 합쳐서 최종 위치 계산
    Vector3 moveDistance = moveForward + moveSide;

    // 3. 리지드바디를 통해 위치 변경
    playerRigidbody.MovePosition(playerRigidbody.position + moveDistance);
}

    // 입력값에 따라 캐릭터를 좌우로 회전
    private void Rotate() {
        // // 상대적으로 회전할 수치 계산
        // float turn =
        //     playerInput.rotate * rotateSpeed * Time.deltaTime;
        // // 리지드바디를 통해 게임 오브젝트 회전 변경
        // playerRigidbody.rotation = playerRigidbody.rotation * Quaternion.Euler(0, turn, 0f);

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
    // 지면(Horizontal Plane)을 가상으로 생성 (Y축이 캐릭터 높이와 같은 평면)
    Plane groundPlane = new Plane(Vector3.up, transform.position);
    float rayDistance;

    // 2. 레이가 평면과 교차하는지 확인
    if (groundPlane.Raycast(ray, out rayDistance)) {
        // 3. 교차 지점(마우스 위치) 좌표 가져오기
        Vector3 lookPoint = ray.GetPoint(rayDistance);
        
        // 4. 캐릭터가 바라볼 방향 계산 (목표 지점 - 현재 위치)
        Vector3 direction = lookPoint - transform.position;
        direction.y = 0; // 하늘이나 땅을 보지 않도록 Y축 고정

        // 5. 리지드바디 회전에 즉시 반영
        if (direction != Vector3.zero) {
            playerRigidbody.rotation = Quaternion.LookRotation(direction);
        }
    }
    }

    
}