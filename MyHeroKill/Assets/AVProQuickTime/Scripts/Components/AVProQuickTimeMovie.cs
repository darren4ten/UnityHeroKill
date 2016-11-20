// Support for DirectX and OpenGL native texture updating, from Unity 4.0 upwards
#if UNITY_4_3 || UNITY_4_2 || UNITY_4_1 || UNITY_4_0_1 || UNITY_4_0
#define AVPRO_UNITY_4_X
#endif

using UnityEngine;
using System.IO;
using System.Collections;

//-----------------------------------------------------------------------------
// Copyright 2012-2013 RenderHeads Ltd.  All rights reserverd.
//-----------------------------------------------------------------------------

[AddComponentMenu("AVPro QuickTime/Movie")]
public class AVProQuickTimeMovie : MonoBehaviour
{
	protected AVProQuickTime _moviePlayer;
	public AVProQuickTimePlugin.MovieSource _source = AVProQuickTimePlugin.MovieSource.LocalFile;
	public string _folder = "";
	public string _filename = "movie.mov";
	public bool _loop = false;
	public bool _allowYUV = true;
	public bool _useYUVHD = true;
	public bool _loadOnStart = true;
	public bool _playOnStart = true;
	//public bool _loadFirstFrame = true;
	public bool _editorPreview = false;
	public float _volume = 1.0f;
	
	[System.NonSerializedAttribute]
	public byte[] _movieData;

	public Texture OutputTexture
	{
		get { if (_moviePlayer != null) return _moviePlayer.OutputTexture; return null; }
	}

	public AVProQuickTime MovieInstance
	{
		get { return _moviePlayer; }
	}

	public void Start()
	{
		if (null == FindObjectOfType(typeof(AVProQuickTimeManager)))
		{
			throw new System.Exception("You need to add AVProQuickTimeManager component to your scene.");
		}
		
		if (_loadOnStart)
		{
			LoadMovie();
		}
	}

	public bool LoadMovie()
	{
		if (_moviePlayer == null)
		{
			_moviePlayer = new AVProQuickTime();
		}
		
		_moviePlayer.IsActive = this.enabled;

		bool loaded = false;
		switch (_source)
		{
			case AVProQuickTimePlugin.MovieSource.LocalFile:
				loaded = _moviePlayer.StartFromFile(Path.Combine(_folder, _filename), _loop, _allowYUV, _useYUVHD);
				break;
			case AVProQuickTimePlugin.MovieSource.URL:
				loaded = _moviePlayer.StartFromURL(Path.Combine(_folder, _filename), _loop, _allowYUV, _useYUVHD);
				break;
			case AVProQuickTimePlugin.MovieSource.Memory:
				if (_movieData != null)
				{
					loaded = _moviePlayer.StartFromMemory(_movieData, _filename, _loop, _allowYUV, _useYUVHD);
				}
				break;
		}

		if (loaded)
		{
			_moviePlayer.Volume = _volume;
		}
		else
		{
			Debug.LogWarning("[AVProQuickTime] Couldn't load movie " + _filename);
			UnloadMovie();
		}

		return loaded;
	}

	public void Update()
	{
		_volume = Mathf.Clamp01(_volume);

		if (_moviePlayer != null)
		{
			if (_volume != _moviePlayer.Volume)
				_moviePlayer.Volume = _volume;
			
			if (!_moviePlayer.IsPlaying)
			{
				/*if (_loadFirstFrame)
				{
					if (_moviePlayer.PlayState == AVProQuickTime.PlaybackState.Loaded)
					{
						_moviePlayer.Frame = 0;
						_loadFirstFrame = false;
					}
				}*/
				if (_playOnStart)
				{
					// Auto play the movie on startup
					if ((int)_moviePlayer.PlayState >= (int)AVProQuickTime.PlaybackState.Loaded && _moviePlayer.LoadedSeconds > 0f)
					{
						_moviePlayer.Play();
						_playOnStart = false;
					}
				}			
			}
			
			if (_moviePlayer.Update(false))
				UpdateFPS();
		}
	}
	
	private int _frameCount;
	public float _fps;
	private float _startFrameTime;
	
	public void UpdateFPS()
	{
		_frameCount++;
		
		float timeNow = Time.realtimeSinceStartup;
		float timeDelta = timeNow - _startFrameTime;
		if (timeDelta >= 1.0f)
		{
			_fps = (float)_frameCount / timeDelta;
			_frameCount  = 0;
			_startFrameTime = timeNow;
		}
	}	

	public void Play()
	{
		if (_moviePlayer != null)
			_moviePlayer.Play();
	}

	public void Pause()
	{
		if (_moviePlayer != null)
			_moviePlayer.Pause();
	}	

	public void UnloadMovie()
	{
		if (_moviePlayer != null)
		{
			_moviePlayer.Dispose();
			_moviePlayer = null;
		}
	}

	public void OnDestroy()
	{
		UnloadMovie();
	}

#if AVPRO_UNITY_4_X
	void OnEnable()
	{
		if (_moviePlayer != null)
		{
			_moviePlayer.IsActive = true;
		}
	}

	void OnDisable()
	{
		if (_moviePlayer != null)
		{
			_moviePlayer.IsActive = false;
		}
	}
#endif

#if UNITY_EDITOR
	[ContextMenu("Save PNG")]
	private void SavePNG()
	{
		if (OutputTexture != null && _moviePlayer != null)
		{
			Texture2D tex = new Texture2D(OutputTexture.width, OutputTexture.height, TextureFormat.ARGB32, false);
			RenderTexture.active = (RenderTexture)OutputTexture;
			tex.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0, false);
			tex.Apply(false, false);
			
			byte[] pngBytes = tex.EncodeToPNG();
			System.IO.File.WriteAllBytes("AVProQuickTime-image" + Random.Range(0, 65536).ToString("X") + ".png", pngBytes);
			
			RenderTexture.active = null;
			Texture2D.Destroy(tex);
			tex = null;
		}
	}
#endif	
}