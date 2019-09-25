using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Transform PlayerTr;

    public Vector3 offset;
    public float smoothing;

    // Start is called before the first frame update
    void Start()
    {
            transform.position = PlayerTr.position + offset;
        
    }

    // Update is called once per frame
    void Update()
    {
        if (smoothing > 0)
        {
            transform.position = Vector3.Slerp(transform.position, PlayerTr.position + offset, Time.deltaTime * smoothing);
        }
        else
        {
            transform.position = PlayerTr.position + offset;
        }
    }
}
