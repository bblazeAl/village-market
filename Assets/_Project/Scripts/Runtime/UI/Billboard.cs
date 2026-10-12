using UnityEngine;

namespace IdleArcade
{
    /// <summary>Keeps world-space UI facing the camera.</summary>
    public class Billboard : MonoBehaviour
    {
        void LateUpdate()
        {
            var cam = CameraRig.Cam != null ? CameraRig.Cam : Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }
    }
}
