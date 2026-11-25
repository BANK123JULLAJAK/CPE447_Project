using UnityEngine;

public class NewmanAnimationEvents : MonoBehaviour
{
    private StarterAssets.ThirdPersonController _controller;

    private void Awake()
    {
        _controller = GetComponentInParent<StarterAssets.ThirdPersonController>();
    }

    private void OnFootstep(AnimationEvent animationEvent)
    {
        if (_controller != null)
        {
            _controller.SendMessage("OnFootstep", animationEvent, SendMessageOptions.DontRequireReceiver);
        }
    }

    private void OnLand(AnimationEvent animationEvent)
    {
        if (_controller != null)
        {
            _controller.SendMessage("OnLand", animationEvent, SendMessageOptions.DontRequireReceiver);
        }
    }
}
