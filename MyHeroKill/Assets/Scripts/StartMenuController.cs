using UnityEngine;
using System.Collections;

public class StartMenuController : MonoBehaviour
{
    public MovieTexture StartMovie;
    public AudioClip AduioSource;
    // Use this for initialization
    void Start()
    {
        StartMovie.loop = false;
        StartMovie.Play();
    }

    // Update is called once per frame
    void Update()
    {

    }

    void OnGUI()
    {
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), StartMovie);
    }
}
