using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TriggerAudio : MonoBehaviour
{
    AudioSource source;
    AudioClip audioClip;
    // Start is called before the first frame update
    void Start()
    {
        //source = GetComponent<AudioSource>();
        //audioClip = GetComponent<AudioClip>();
    }

    private void OnTriggerEnter(Collider other)
    {
        source = GetComponent<AudioSource>();
        source.Play();
    }
}
