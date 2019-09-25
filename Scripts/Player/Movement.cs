using UnityEngine;

public class Movement : MonoBehaviour
{
    public float RunSpeed;
    public float RunAcceleration = 5;
    public float AirDrag = 0.4f;
    public Animator anim;
    public CharacterController chc;




    [Header("Jumping")]
    public KeyCode JumpKey = KeyCode.W;
    public Vector3 GroundBoxCastCenter;
    public float GroundBoxCastLength = 0.3f;
    public int ExtraJumps = 1;
    public float JumpPower = 10;

    public float SideRayLenght;
    public Vector3 SideRayOffsetR;
    public Vector3 SideRayOffsetL;
    public int obstacleLayer;


    public const float Gravity = 7;
    public const float WalljumpVelocityChange = -0.7f;


    #region Private Fields

    private float _gravAcc;
    private float _horiz;
    private int _avaibleJumps = 1;
    private float _jumpCD;
    private int _hitSide;

    private float _horVel;

    #endregion

    // Start is called before the first frame update
    private void Start()
    {

    }

    // Update is called once per frame
    private void Update()
    {
        GetInput();
        _hitSide = CheckTouchingSides();
        HandleJumping();
        HandleRunning();

    }

    private void HandleRunning()
    {
        float dt = Time.deltaTime;
        if (_horiz != 0)
        {
            _horVel = Mathf.Clamp(_horVel + RunAcceleration * Time.deltaTime * _horiz, -RunSpeed, RunSpeed);
            anim.SetBool("running", true);
            anim.SetLayerWeight(1, Mathf.Abs(_horVel) / RunSpeed);

        }
        else
        {
            anim.SetLayerWeight(1, 0);
            anim.SetBool("running", false);
            if (chc.isGrounded)// a lot of friction
            {
                _horVel -= dt * RunAcceleration * Mathf.Sign(_horVel);
            }
            else
            {
                _horVel -= dt * AirDrag * Mathf.Sign(_horVel);

            }
        }
        //CheckInstantStops();



        Run();

    }

    private void Run()
    {
        Vector3 movement = new Vector3(_horVel, -_gravAcc, 0) * Time.deltaTime;

        chc.Move(movement);
    }

    private void CheckInstantStops()
    {
        if (_hitSide != 0)
        {
            //raycast left
            if (_horVel > 0 && _hitSide == 1)
            {
                //stop
                _horVel = 0;
            }
            else if (_horVel < 0 && _hitSide == -1)
            {
                //stop
                _horVel = 0;
            }
        }
    }


    //used in instant stopping if ramming into an obstacle and wall jumping
    private int CheckTouchingSides()
    {

        Debug.DrawRay(transform.position + SideRayOffsetR, Vector3.right * SideRayLenght, Color.red);
        Debug.DrawRay(transform.position + SideRayOffsetL, Vector3.left * SideRayLenght, Color.red);
        if (Physics.Raycast(transform.position + SideRayOffsetR, Vector3.right, SideRayLenght, obstacleLayer))
        {
            //right
            return 1;
        }
        else if (Physics.Raycast(transform.position + SideRayOffsetL, Vector3.left, SideRayLenght, obstacleLayer))
        {
            //left
            return -1;
        }

        return 0;
    }

    private void HandleJumping()
    {
        if (Input.GetKeyDown(JumpKey) && _jumpCD <= 0)
        {
            //check if grounded or in air and has avaible second jump

            if (Physics.BoxCast(transform.position + GroundBoxCastCenter, Vector3.one * 0.4f, Vector3.down, Quaternion.identity, GroundBoxCastLength))
            {
                Debug.Log("perform initial jump");
                Jump();


            }
            else if ((_hitSide == 1 && _horVel > 1) || (_hitSide == -1 && _horVel < -1))
            {
                Jump();
                _horVel *= WalljumpVelocityChange;
            }
            else if (_avaibleJumps > 0)
            {
                Debug.Log("perform secondary jump");
                _avaibleJumps--;
                Jump();
            }
        }
        else
        {
            _jumpCD -= Time.deltaTime;
        }
    }

    private void Jump()
    {
        _gravAcc = -JumpPower;
        _jumpCD = 0.1f;
    }

    private void GetInput()
    {
        _horiz = Input.GetAxis("Horizontal");
    }

    private void FixedUpdate()
    {
        HandleGravity();
    }


    public void HandleGravity()
    {

//CollisionFlags cf = (CollisionFlags)6;
//cf & CollisionFlags.Above // returns above or none

//CollisionFlags cf2 = CollisionFlags.Above | CollisionFlags.Below | CollisionFlags.Sides;
//(int)cf2 //returns 7

        if (_gravAcc < 0 && (chc.collisionFlags & CollisionFlags.Above) == CollisionFlags.Above)
        {
            Debug.Log("Hit head");
            _gravAcc = 0;
        }
        bool jumping = _gravAcc < 0;
        if (!chc.isGrounded || jumping)
        {

            if (jumping)
            {
                _gravAcc -= (_gravAcc - Gravity) * Time.fixedDeltaTime;
            }
            else
            {
                _gravAcc += (_gravAcc + Gravity) * Time.fixedDeltaTime;

            }
        }
        else
        {
            Landed();
        }
    }

    private void Landed()
    {
        if (_gravAcc > 0)
        {
            _gravAcc = 0;
            _avaibleJumps = ExtraJumps;
            _jumpCD = 0;
        }
    }
}
