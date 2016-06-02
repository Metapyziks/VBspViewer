using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using JetBrains.Annotations;
using Valve.VR;

public class DragLocomotion : MonoBehaviour
{
    private class ControllerInfo
    {
        private readonly SteamVR_TrackedObject _trackedObject;
        private SteamVR_Controller.Device _device;

        public bool IsValid { get { return _trackedObject.isValid; } }

        public SteamVR_Controller.Device Controller
        {
            get
            {
                if (!IsValid) return null;
                return _device ?? (_device = SteamVR_Controller.Input((int) _trackedObject.index));
            }
        }

        public Transform Transform { get { return _trackedObject.transform; } }

        public ControllerInfo(GameObject go)
        {
            _trackedObject = go.GetComponent<SteamVR_TrackedObject>();
        }
    }

    private SteamVR_ControllerManager _controllerManager;

    [UsedImplicitly]
    private void Start()
    {
        _controllerManager = FindObjectOfType<SteamVR_ControllerManager>();
    }

    private readonly Dictionary<GameObject, ControllerInfo>
        _controllerCache = new Dictionary<GameObject, ControllerInfo>();

    private ControllerInfo GetControllerInfo(GameObject go)
    {
        ControllerInfo info;
        if (_controllerCache.TryGetValue(go, out info)) return info;

        info = new ControllerInfo(go);
        _controllerCache.Add(go, info);

        return info;
    }
        
    [UsedImplicitly]
	private void Update ()
    {
        if (_controllerManager == null) return;

        CheckForGrabUpdates();
        UpdateTransform();
    }

    private void CheckForGrabUpdates()
    {
        foreach (var go in _controllerManager.objects)
        {
            var info = GetControllerInfo(go);
            if (!info.IsValid) continue;

            if (info.Controller.GetPress(EVRButtonId.k_EButton_Grip))
            {
                OnControllerGrab(info);
            }
            else
            {
                OnControllerRelease(info);
            }
        }
    }

    private readonly Dictionary<ControllerInfo, Vector3> _grabPoints
        = new Dictionary<ControllerInfo, Vector3>(); 

    private void OnControllerGrab(ControllerInfo info)
    {
        if (_grabPoints.ContainsKey(info)) return;
        _grabPoints.Add(info, info.Transform.position); 
    }

    private void OnControllerRelease(ControllerInfo info)
    {
        if (!_grabPoints.ContainsKey(info)) return;
        _grabPoints.Remove(info);

        foreach (var key in _grabPoints.Keys.ToArray())
        {
            _grabPoints[key] = key.Transform.position;
        }
    }

    private void UpdateTransform()
    {
        var first = _grabPoints.FirstOrDefault();
        var second = _grabPoints.Skip(1).FirstOrDefault();

        if (first.Key == null) return;
        if (second.Key == null)
        {
            UpdateTranslation(first.Key.Transform.position, first.Value);
        }
        else
        {
            var meanCurPos = (first.Key.Transform.position + second.Key.Transform.position) * 0.5f;
            var meanDstPos = (first.Value + second.Value) * 0.5f;
            UpdateTranslation(meanCurPos, meanDstPos);

            var worldDiff = first.Key.Transform.position - second.Key.Transform.position;
            var localDiff = first.Value - second.Value;

            UpdateRotation(meanDstPos, worldDiff, localDiff);
            UpdateScale(worldDiff, localDiff);
        }
    }

    private void UpdateTranslation(Vector3 curPos, Vector3 dstPos)
    {
        transform.position += dstPos - curPos;
    }

    private void UpdateRotation(Vector3 origin, Vector3 curVec, Vector3 dstVec)
    {
        var curAng = Mathf.Atan2(curVec.z, curVec.x)* Mathf.Rad2Deg;
        var dstAng = Mathf.Atan2(dstVec.z, dstVec.x)* Mathf.Rad2Deg;
        transform.RotateAround(origin, Vector3.up, Mathf.DeltaAngle(dstAng, curAng));
    }

    private void UpdateScale(Vector3 curVec, Vector3 dstVec)
    {
        var curLen = curVec.magnitude;
        var dstLen = dstVec.magnitude;

        transform.localScale *= dstLen / curLen;
    }
}
