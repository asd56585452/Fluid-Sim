using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class RenderDrog : MonoBehaviour
{
    // Start is called before the first frame update
    public Transform FollowTransform;
    public float Drag=0.1f;
    public float3 DragTransformPosition;
    public Quaternion DragTransformRotation;
    void Start()
    {
        if(FollowTransform == null)
        {
            FollowTransform = transform.parent;
        }
        DragTransformPosition = transform.position;
        DragTransformRotation = transform.rotation;
    }

    // Update is called once per frame
    void Update()
    {
        float TDrag = math.pow(Drag, Time.deltaTime);
        DragTransformPosition = Vector3.Slerp(FollowTransform.position, DragTransformPosition, TDrag);
        DragTransformRotation = Quaternion.Slerp(FollowTransform.rotation, DragTransformRotation, TDrag);
        transform.position = DragTransformPosition;
        transform.rotation = DragTransformRotation;
    }
}
