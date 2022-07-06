using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class drawLine : MonoBehaviour
{
    private Vector3 _prevPos = Vector3.zero;
    
    [SerializeField] private float _drawDuration = 1.5f;

    // Update is called once per frame
    void Update()
    {
        Debug.DrawLine(_prevPos, transform.position, Color.red, _drawDuration);
        _prevPos = transform.position;
    }
}
