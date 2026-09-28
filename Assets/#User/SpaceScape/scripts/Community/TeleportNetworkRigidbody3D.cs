using Fusion.Addons.Physics;
using System;
using System.Collections;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Community
{
    public class TeleportNetworkRigidbody3D : MonoBehaviour
    {
        [SerializeField] private NetworkRigidbody3D _networkObject;
        [SerializeField] private Rigidbody _rigidbody;
        [SerializeField] private Transform _teleportTarget;
        [SerializeField] private bool _requestOwnershipOnTeleport = true;
        [SerializeField] private bool _conserveVelocity = false;

        private readonly float TIMEOUT = 2.0f;

        public void Teleport()
        {
            if (_teleportTarget == null) // No target assigned, abort
                return;

            if (_networkObject.HasStateAuthority)
                DoTeleport();
            else if(_requestOwnershipOnTeleport)
            {
                StartCoroutine(AsyncGetStateAuthority(DoTeleport));
            }
        }

        public void SetConserveVelocity(bool conserveVelocity)
        {
            _conserveVelocity = conserveVelocity;
        }

        private void DoTeleport()
        {
            Vector3 targetLocation = _teleportTarget.position;
            Quaternion targetRotation = _teleportTarget.rotation;

            if (!_conserveVelocity)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }

            if (Application.isEditor || _networkObject.HasStateAuthority)
            {
                _rigidbody.position = targetLocation;
                _rigidbody.rotation = targetRotation;
                _networkObject.ResetState();
            }
        }

        private IEnumerator AsyncGetStateAuthority(Action _onAuthorityReceived)
        {
            _networkObject.Object.RequestStateAuthority();

            // # Setup to correct the non kinematic state
            float startTime = Time.time;
            while (!_networkObject.HasStateAuthority && Time.time - startTime < TIMEOUT)
            {
                if (Time.time - startTime >= TIMEOUT)
                {
                    Debug.LogWarning($"[{nameof(NetworkGrabbable)}] Timeout, can't get object control");
                    yield break;
                }
                yield return new WaitForFixedUpdate();
            }

            if(_networkObject.HasStateAuthority)
                _onAuthorityReceived!.Invoke();
        }

        private void OnValidate()
        {
            if (_networkObject == null)
                _networkObject = GetComponent<NetworkRigidbody3D>();
            if (_networkObject != null && _rigidbody == null)
                _rigidbody = _networkObject.GetComponent<Rigidbody>();
        }
    }
}
