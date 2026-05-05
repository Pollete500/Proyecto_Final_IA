using UnityEngine;

namespace KartGame.Kart
{
    /*
     * Script: CameraFollow.cs
     * Purpose: Keeps the camera behind the controlled kart with smooth motion and a small velocity-based look-ahead.
     * Attach To: Main Camera.
     * Required Components: Camera.
     * Dependencies: A target Transform, typically the player kart.
     * Inspector Setup: Assign the player kart as target and tune offset and smoothing to match the desired arcade feeling.
     */
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 4.25f, -7.5f);
        [SerializeField] private Vector3 lookOffset = new Vector3(0f, 1.5f, 0f);
        [SerializeField] private float followSmoothTime = 0.15f;
        [SerializeField] private float rotationLerpSpeed = 8f;
        [SerializeField] private float velocityLookAhead = 1.5f;

        private Vector3 _followVelocity;
        private KartController _targetKartController;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            _targetKartController = target != null ? target.GetComponent<KartController>() : null;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            if (_targetKartController == null)
            {
                _targetKartController = target.GetComponent<KartController>();
            }

            var speedFactor = _targetKartController != null
                ? Mathf.Clamp01(_targetKartController.GetCurrentSpeed() / Mathf.Max(0.01f, _targetKartController.MaxSpeed))
                : 0f;

            var lookAhead = target.forward * velocityLookAhead * speedFactor;
            var desiredPosition = target.TransformPoint(offset) + lookAhead;
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _followVelocity, followSmoothTime);

            var desiredLookTarget = target.position + lookOffset + lookAhead * 0.4f;
            var desiredRotation = Quaternion.LookRotation(desiredLookTarget - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationLerpSpeed * Time.deltaTime);
        }
    }
}
