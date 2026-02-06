using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.CompilerServices;
using Unity.VisualScripting;
using UnityEditor.Build;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.Assertions.Must;
using UnityEngine.InputSystem;

public class OneWayPlatform : MonoBehaviour
{
    public GameObject player;
    public Rigidbody2D rb;
    public PlayerInput playerInput;
    public Collider2D col;
    public bool isDisabled;
    public Player playerScript;
    public float cooldown;
    public float timeSinceDisable;
    public bool platformIsBelow;
    public LayerMask playerLayer;
    public float disableTime;
    public float dropVelocity;

    void Start()
    {
        isDisabled = false;
    }

    void FixedUpdate()
    {
        timeSinceDisable += Time.deltaTime;

        platformIsBelow = playerScript.platformBelow;

        if(playerScript.dropPressed)
        {
            playerScript.dropPressed = false;
            timeSinceDisable = 0f;

            _ = Disable();
        }
    }


    private async UniTaskVoid Disable()
    {
        if(!platformIsBelow) return;
        if(isDisabled) return;

        isDisabled = true;

        col.enabled = false;

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, dropVelocity);

        await UniTask.WaitUntil(() => platformIsBelow == false);

        await UniTask.Delay((int)(disableTime * 1000));

        col.enabled = true;
        isDisabled = false;
    }
}
