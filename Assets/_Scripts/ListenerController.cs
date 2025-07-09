using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(CharacterController))]
public class ListenerController : MonoBehaviour
{
    [Header("Control Settings")]
    [SerializeField] private float speed;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float jumpHeight;
    [SerializeField] private float lookSensitivity;

    [Header("Placement Settings")]
    [Range(0, 50)]
    [SerializeField] private float maxPlaceDistance = 10f;
    [Range(0, 200)]
    [SerializeField] private float maxRemoveDistance = 100f;

    [Header("References")]
    [SerializeField] private SceneData sceneData;
    [SerializeField] private Text selectionText;

    [Header("Respawn Settings")]
    [SerializeField] private float fallThresholdY = -20f;
    [SerializeField] private Vector3 respawnPosition = Vector3.zero;


    private CharacterController controller;
    private Camera playerCamera;
    private Vector3 velocity;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private float xRotation = 0f;
    private bool isCursorLocked = true;
    private bool groundedPlayer;

    private AudioClip[] audioClips;
    private int currentClipIndex = 0;

    void Awake()
    {
        // Init
        controller = GetComponent<CharacterController>();
        playerCamera = transform.Find("Camera").GetComponent<Camera>();
        Cursor.visible = false;

        // Load audio files
        audioClips = Resources.LoadAll<AudioClip>("Audio");
        UpdateSelectionText();

        // DEBUGGING
        string audioFilesString = "";
        foreach (var t in audioClips)
        {
            audioFilesString += t.name + " ";
        }
        Debug.Log($"ListenerController: {audioClips.Length} AudioClips loaded: {audioFilesString}");
    }

    void Update()
    {
        MoveAround();
        LookAround();
        CheckFallReset();
    }

    public void OnSwitch(InputValue value)
    {
        if (isCursorLocked) sceneData.SwitchSimulation();
    }

    public void OnMove(InputValue value)
    {
        if (isCursorLocked) moveInput = value.Get<Vector2>();
    }

    public void OnSprint(InputValue value)
    {
        if (isCursorLocked) moveInput *= 2;
    }

    public void OnLook(InputValue value)
    {
        if (isCursorLocked) lookInput = value.Get<Vector2>();
    }

    public void OnJump(InputValue value)
    {
        if (controller.isGrounded && isCursorLocked) velocity.y += Mathf.Sqrt(jumpHeight * -2.0f * gravity);
    }

    public void OnToggleMouseLock(InputValue value)
    {
        isCursorLocked = !isCursorLocked;
        Cursor.lockState = isCursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !isCursorLocked;
    }

    private void LookAround()
    {
        float mouseX = lookInput.x * lookSensitivity * Time.deltaTime;
        float mouseY = lookInput.y * lookSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        playerCamera.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    private void MoveAround()
    {
        groundedPlayer = controller.isGrounded;
        if (groundedPlayer && velocity.y < 0)
        {
            velocity.y = 0f;
        }

        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        controller.Move(move * Time.deltaTime * speed);

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private void CheckFallReset()
    {
        if (transform.position.y < fallThresholdY)
        {
            Debug.Log("Player fell off the map. Resetting position.");
            controller.enabled = false; // Temporarily disable to avoid CharacterController conflicts
            transform.position = respawnPosition;
            velocity = Vector3.zero; // Reset vertical velocity
            controller.enabled = true;
        }
    }

    public void OnPlace(InputValue value)
    {
        PlaceAudioSource();
    }

    public void OnRemove(InputValue value)
    {
        RemoveAudioSource();
    }

    public void OnNextAudio(InputValue value)
    {
        currentClipIndex = (currentClipIndex + 1) % audioClips.Length;
        UpdateSelectionText();
    }

    public void OnPreviousAudio(InputValue value)
    {
        currentClipIndex = (currentClipIndex - 1 + audioClips.Length) % audioClips.Length;
        UpdateSelectionText();
    }

    private void UpdateSelectionText()
    {
        if (selectionText != null && audioClips.Length > 0)
        {
            selectionText.text = "Selected: " + audioClips[currentClipIndex].name;
        }
        else
        {
            Debug.LogError("ListenerController: Found no SelectionText.");
        }
    }

    private void PlaceAudioSource()
    {
        if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out RaycastHit hit, maxPlaceDistance))
        {
            sceneData.AddSourceToScene(hit.point + hit.normal * 0.5f, audioClips[currentClipIndex]);
        }
        else
        {
            // Debug.Log("ListenerController: No Geometry to place AudioSource on.");
        }
    }

    private void RemoveAudioSource()
    {
        if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out RaycastHit hit, maxRemoveDistance))
        {

            AudioSource audioSource = hit.collider.GetComponent<AudioSource>();

            if (audioSource != null)
            {
                sceneData.RemoveSourceFromScene(audioSource);
                //Debug.Log("ListenerController: Removed AudioSource.");
            }
            else
            {
                //Debug.Log("ListenerController: No AudioSource found.");
            }
        }
    }
}
