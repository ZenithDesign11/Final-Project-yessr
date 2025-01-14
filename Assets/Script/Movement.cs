using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerMovement : MonoBehaviour
{
    private Vector2 movement;
    private float speed = 8f;
    private float originalSpeed = 8f;
    private float jumpingPower = 10f;
    private float originalJumpingPower = 10f;
    private bool isFacingRight = true;
    private bool isSpeedBoostActive = false;
    private bool isTeleportOnCooldown = false;
    private bool isRewindOnCooldown = false;

    private Rigidbody2D rb;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask obstacleLayer;

    private Camera mainCamera;

    // For recording player movement
    private Queue<Vector3> positionHistory = new Queue<Vector3>();
    private float recordInterval = 0.1f; // Record position every 0.1 seconds
    private float rewindSpeed = 5f; // Speed multiplier for rewind
    private float lastGroundedHeight;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        mainCamera = Camera.main;
        StartCoroutine(RecordPosition());
    }

    void Update()
    {
        // Handle Horizontal Movement
        movement.x = Input.GetAxisRaw("Horizontal");

        // Jump with Spacebar
        if (Input.GetKeyDown(KeyCode.Space) && IsGrounded())
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpingPower);
            lastGroundedHeight = transform.position.y; // Track height before jumping
        }

        // Speed Boost with Q
        if (Input.GetKeyDown(KeyCode.Q) && !isSpeedBoostActive)
        {
            StartCoroutine(SpeedBoost());
        }

        // Teleport on Mouse Click with Collision Detection and Cooldown
        if (Input.GetMouseButtonDown(0) && !isTeleportOnCooldown)
        {
            StartCoroutine(TeleportWithCooldown());
        }

        // Rewind with X
        if (Input.GetKeyDown(KeyCode.X) && !isRewindOnCooldown)
        {
            StartCoroutine(Rewind());
        }

        Flip();
        CheckIfOutOfCamera();

        // Check for fall damage
        if (IsGrounded() && lastGroundedHeight - transform.position.y > 20f)
        {
            StartCoroutine(ShakeCamera());
        }
    }

    private void FixedUpdate()
    {
        // Adjust speed based on grounded state
        if (IsGrounded())
        {
            rb.linearVelocity = new Vector2(movement.x * speed, rb.linearVelocity.y);
        }
        else
        {
            rb.linearVelocity = new Vector2(movement.x * 8f, rb.linearVelocity.y); // Limit speed to 8f mid-air
        }
    }

    private bool IsGrounded()
    {
        return Physics2D.OverlapCircle(groundCheck.position, 0.2f, groundLayer);
    }

    private void Flip()
    {
        if (isFacingRight && movement.x < 0f || !isFacingRight && movement.x > 0f)
        {
            isFacingRight = !isFacingRight;
            Vector3 localScale = transform.localScale;
            localScale.x *= -1f;
            transform.localScale = localScale;
        }
    }

    private IEnumerator SpeedBoost()
    {
        isSpeedBoostActive = true;

        // Triple speed and jump force
        speed = originalSpeed * 3;
        jumpingPower = originalJumpingPower * 2;

        yield return new WaitForSeconds(10); // Active duration of speed boost

        // Reset speed and jump force
        speed = originalSpeed;
        jumpingPower = originalJumpingPower;

        yield return new WaitForSeconds(10); // Cooldown for speed boost
        isSpeedBoostActive = false;
    }

    private void CheckIfOutOfCamera()
    {
        Vector3 viewportPosition = mainCamera.WorldToViewportPoint(transform.position);

        if (viewportPosition.x < 0 || viewportPosition.x > 1 || viewportPosition.y < 0 || viewportPosition.y > 1)
        {
            StartCoroutine(ShakeCamera());
            Die();
        }
    }

    private void Die()
    {
        Debug.Log("Player died!");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator TeleportWithCooldown()
    {
        isTeleportOnCooldown = true;

        // Perform teleportation
        TeleportWithCollisionCheck();

        // Cooldown for 14 seconds
        yield return new WaitForSeconds(14);

        isTeleportOnCooldown = false;
    }

    private void TeleportWithCollisionCheck()
    {
        // Get the mouse position in world coordinates
        Vector3 mousePosition = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = transform.position.z; // Keep the player's Z position constant

        // Direction vector from the player to the mouse position
        Vector2 direction = (mousePosition - transform.position).normalized;

        // Distance to the mouse position
        float distance = Vector2.Distance(transform.position, mousePosition);

        // Limit the teleportation distance to 22 units
        if (distance > 18f)
        {
            distance = 18f; // Cap the distance
            mousePosition = (Vector2)transform.position + direction * distance; // Adjust target position
        }

        // Raycast to check for obstacles
        RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, distance, obstacleLayer);

        if (hit.collider != null)
        {
            // If an obstacle is hit, teleport to the point of collision
            transform.position = hit.point;
            Debug.Log($"Teleported to obstacle at: {hit.point}");
        }
        else
        {
            // If no obstacle is hit, teleport to the adjusted mouse position
            transform.position = mousePosition;
            Debug.Log($"Teleported to: {mousePosition}");
        }
    }

    private IEnumerator RecordPosition()
    {
        while (true)
        {
            // Record the current position
            positionHistory.Enqueue(transform.position);

            // Keep only the last 10 seconds of positions
            if (positionHistory.Count > 100) // 10 seconds / 0.1 interval = 100 positions
            {
                positionHistory.Dequeue();
            }

            yield return new WaitForSeconds(recordInterval);
        }
    }

    private IEnumerator Rewind()
    {
        isRewindOnCooldown = true;

        Debug.Log("Rewinding...");

        // Temporarily disable player controls during rewind
        enabled = false;

        Vector3[] positions = positionHistory.ToArray();
        for (int i = positions.Length - 1; i >= 0; i--)
        {
            transform.position = positions[i];
            yield return new WaitForSeconds(recordInterval / rewindSpeed);
        }

        // Clear the position history after rewind
        positionHistory.Clear();

        // Re-enable player controls
        enabled = true;

        Debug.Log("Rewind complete.");

        yield return new WaitForSeconds(20); // Cooldown for rewind
        isRewindOnCooldown = false;
    }

    private IEnumerator ShakeCamera()
    {
        float shakeDuration = 1.0f; // Shake duration
        float shakeMagnitude = 1.0f; // Intensity of shake

        Vector3 originalPosition = mainCamera.transform.position;

        float elapsed = 0.0f;

        while (elapsed < shakeDuration)
        {
            float offsetX = Random.Range(-shakeMagnitude, shakeMagnitude);
            float offsetY = Random.Range(-shakeMagnitude, shakeMagnitude);

            mainCamera.transform.position = new Vector3(originalPosition.x + offsetX, originalPosition.y + offsetY, originalPosition.z);

            elapsed += Time.deltaTime;
            yield return null;
        }

        mainCamera.transform.position = originalPosition;
    }
}
