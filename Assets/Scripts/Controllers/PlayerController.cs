using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{

    private List<Wisard> wisardsList;
    private Wisard currentWisard;
    //private Dictionary<Wisard,>

    public Action PlayerUpRequest;
    public Action PlayerDownRequest;

    [SerializeField] private int rage;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    private void OnEnable()
    {
        Lane.WisardEnteredZone += HandleWisardEntered;
        Lane.WisardExitedZone += HandleWisardExited;
    }

    private void OnDisable()
    {
        Lane.WisardEnteredZone -= HandleWisardEntered;
        Lane.WisardExitedZone -= HandleWisardExited;
    }

    private void HandleWisardExited(Wisard wisard, Lane lane)
    {
        throw new NotImplementedException();
    }

    private void HandleWisardEntered(Wisard wisard, Lane lane)
    {
        throw new NotImplementedException();
    }

    // Update is called once per frame
    void Update()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            ChangeCurrentWisard();
        }

        if (Keyboard.current.shiftKey.wasPressedThisFrame)
        {
            //TODO: Büyüleme fonksyionu
        }

        if (Keyboard.current.upArrowKey.wasPressedThisFrame)
        {
            PlayerUpRequest?.Invoke();
        }

        if (Keyboard.current.downArrowKey.wasPressedThisFrame)
        {
            PlayerDownRequest?.Invoke();
        }
    }

    private void ChangeCurrentWisard() { 
    
    }

}
