using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class TriggerAudio : MonoBehaviour
{
    //private AudioClip audioClip;
    private AudioSource source;

    void Awake()
    {
        source = GetComponent<AudioSource>();

        if (source == null) Debug.LogWarning($"{name} has no audioSource component");
        //if (audioClip == null) Debug.LogWarning($"{name} has no audioClip assigned");

        //source.clip = audioClip;
        if (source.clip == null) Debug.LogWarning($"{name} audioSource has no clip assigned");
    }

    private void OnTriggerEnter(Collider other)
    {
        source.Play();
    }

    private void OnTriggerExit(Collider other)
    {
        source.Stop();
    }
}
