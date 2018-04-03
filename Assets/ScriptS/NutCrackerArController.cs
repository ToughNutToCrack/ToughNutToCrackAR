using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using GoogleARCore;
using GoogleARCore.HelloAR;
using UnityEngine;

public class NutCrackerArController : MonoBehaviour {

    public Camera FirstPersonCamera;
    public GameObject TrackedPlanePrefab;
    public GameObject prefabToIstantiate;
    public GameObject SearchingForPlaneUI;

    private List<TrackedPlane> m_NewPlanes = new List<TrackedPlane>();
    private List<TrackedPlane> m_AllPlanes = new List<TrackedPlane>();
    
    private List<GameObject> instantiatedPlanes = new List<GameObject> ();
    private List<GameObject> instantiatedObjects = new List<GameObject>();
    
    private bool m_IsQuitting = false;
    const int lostTrackingSleepTimeout = 15;

    
    [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
    [SuppressMessage("ReSharper", "InvertIf")]
    [SuppressMessage("ReSharper", "LoopCanBeConvertedToQuery")]
    public void Update(){
        if (Input.GetKey(KeyCode.Escape)){
            Application.Quit();
        } 

        _QuitOnConnectionErrors();

        if (Session.Status != SessionStatus.Tracking) {
            Screen.sleepTimeout = lostTrackingSleepTimeout;
            if (!m_IsQuitting && Session.Status.IsValid()){
                SearchingForPlaneUI.SetActive(true);
            }
            lostTrackingBehaviour();
            return;
        }

        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        Session.GetTrackables<TrackedPlane>(m_NewPlanes, TrackableQueryFilter.New);
        
        for (int i = 0; i < m_NewPlanes.Count; i++){
            if (instantiatedObjects.Count < 1){
                GameObject planeObject = Instantiate(
                    TrackedPlanePrefab, 
                    Vector3.zero, 
                    Quaternion.identity,
                    transform
                );
                planeObject.GetComponent<TrackedPlaneVisualizer>().Initialize(m_NewPlanes[i]);
                instantiatedPlanes.Add(planeObject);
            }
        }

        Session.GetTrackables<TrackedPlane>(m_AllPlanes);
        bool showSearchingUi = true;
        
        for (int i = 0; i < m_AllPlanes.Count; i++){
            if (m_AllPlanes[i].TrackingState == TrackingState.Tracking){
                showSearchingUi = false;
                break;
            }
        }

        if (!showSearchingUi){
            showAllPlanes();
        }

        SearchingForPlaneUI.SetActive(showSearchingUi);
        
        if (instantiatedObjects.Count >= 1){
            hideAllPlanes();
        }

        Touch touch;
        if (Input.touchCount < 1 || (touch = Input.GetTouch(0)).phase != TouchPhase.Began){
            return;
        }
        

        TrackableHit hit;
        TrackableHitFlags raycastFilter = TrackableHitFlags.PlaneWithinPolygon |
            TrackableHitFlags.FeaturePointWithSurfaceNormal;

        if (Frame.Raycast(touch.position.x, touch.position.y, raycastFilter, out hit)){
            if (instantiatedObjects.Count < 1){
                var obj = Instantiate(prefabToIstantiate, hit.Pose.position, hit.Pose.rotation);

                var anchor = hit.Trackable.CreateAnchor(hit.Pose);

                if ((hit.Flags & TrackableHitFlags.PlaneWithinPolygon) != TrackableHitFlags.None){
                    Vector3 cameraPositionSameY = FirstPersonCamera.transform.position;
                    cameraPositionSameY.y = hit.Pose.position.y;

                    obj.transform.LookAt(cameraPositionSameY, obj.transform.up);
                }

                obj.transform.parent = anchor.transform;
                instantiatedObjects.Add(obj);
            }
        }
    }
    
    private void _QuitOnConnectionErrors(){
        if (m_IsQuitting)
        {
            return;
        }

        if (Session.Status == SessionStatus.ErrorPermissionNotGranted){
            _ShowAndroidToastMessage("Camera permission is needed to run this application.");
            m_IsQuitting = true;
            Invoke("_DoQuit", 0.5f);
        }
        else if (Session.Status.IsError()){
            _ShowAndroidToastMessage("ARCore encountered a problem connecting.  Please start the app again.");
            m_IsQuitting = true;
            Invoke("_DoQuit", 0.5f);
        }
    }

    private void _DoQuit(){
        Application.Quit();
    }

    private void _ShowAndroidToastMessage(string message){
        AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        AndroidJavaObject unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

        if (unityActivity != null)
        {
            AndroidJavaClass toastClass = new AndroidJavaClass("android.widget.Toast");
            unityActivity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
            {
                AndroidJavaObject toastObject = toastClass.CallStatic<AndroidJavaObject>("makeText", unityActivity,
                    message, 0);
                toastObject.Call("show");
            }));
        }
    }
    
    private void hideAllPlanes(){
        foreach (GameObject plane in instantiatedPlanes){
            plane.SetActive(false);
        }
    }

    private void showAllPlanes(){
        foreach (GameObject plane in instantiatedPlanes){
            plane.SetActive(true);
        }
    }

    private void destoryAllObjects(){
        foreach (GameObject obj in instantiatedObjects){
            Destroy(obj);
        }
        instantiatedObjects.Clear();
    }

    private void lostTrackingBehaviour(){
        destoryAllObjects();
        hideAllPlanes();
    }
    
}
