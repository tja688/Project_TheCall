using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Demo-only switch between the three reference screens. Digit keys 1, 2 and 3.
/// </summary>
public sealed class HearthwoodDemoScreens : MonoBehaviour
{
    [SerializeField] GameObject[] screens;
    [SerializeField] int index;

    void OnEnable()
    {
        Show(index);
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || screens == null)
            return;

        if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
            Show(0);
        else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
            Show(1);
        else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
            Show(2);
    }

    public void Show(int next)
    {
        if (screens == null || screens.Length == 0)
            return;

        index = Mathf.Clamp(next, 0, screens.Length - 1);
        for (int i = 0; i < screens.Length; i++)
        {
            if (screens[i] != null)
                screens[i].SetActive(i == index);
        }
    }
}
