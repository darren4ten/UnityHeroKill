// Support for DirectX and OpenGL native texture updating, from Unity 4.0 upwards
#if UNITY_4_3 || UNITY_4_2 || UNITY_4_1 || UNITY_4_0_1 || UNITY_4_0
#define AVPRO_UNITY_4_X
#endif

using UnityEngine;
using System;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;

//-----------------------------------------------------------------------------
// Copyright 2012-2013 RenderHeads Ltd.  All rights reserverd.
//-----------------------------------------------------------------------------

public class AVProQuickTime : System.IDisposable
{
	public enum PlaybackState
	{
		Unknown,
		Loading,
		Loaded,
		Playing,
		Stopped,
	};
	
	//-----------------------------------------------------------------------------

	private int _movieHandle = -1;
	private AVProQuickTimeFormatConverter _formatConverter;
	private AVProQuickTimePlugin.MovieSource _movieSource;
	private uint _lastFrameDrawn = 0xffffffff;
	private bool _isActive = false;
	private bool _yuvHD = true;
	
	// Loading from memory
	private GCHandle _movieMemoryHandle;
	private IntPtr _movieMemoryPtr;
	private UInt32 _movieMemoryLength;
	
	//-----------------------------------------------------------------------------
	
	public bool IsActive {
		get
		{
			return _isActive;
		}
		set
		{
			_isActive = value;
#if AVPRO_UNITY_4_X
			if (_movieHandle >= 0)
				AVProQuickTimePlugin.SetActive(_movieHandle, _isActive);
#endif
		}
	}
	
	public int Handle
	{
		get { return _movieHandle; }
	}

	public PlaybackState PlayState
	{
		get; private set;
	}
	
	protected bool IsPrepared
	{
		get; private set;
	}
	
	public bool IsPaused
	{
		get; private set;
	}	
	
	protected AVProQuickTimePlugin.PixelFormat PixelFormat
	{
		get; private set;
	}
	
	public float LoadedSeconds
	{
		get { return AVProQuickTimePlugin.GetLoadedFraction(_movieHandle) * DurationSeconds; }
	}

	// Movie Properties
	
	public string Filename
	{
		get; private set;
	}
	
	public int Width
	{
		get; private set;
	}
	
	public int Height
	{
		get; private set;
	}

	public float AspectRatio
	{
		get { return (Width / (float)Height); }
	}

	public float FrameRate
	{
		get; private set;
	}
	
	public float DurationSeconds
	{
		get; private set;
	}	

	public uint FrameCount
	{
		get; private set;
	}
	
	private bool IsVisual
	{
		get; set;
	}
	
	// Movie State
	
	public bool IsPlaying
	{
		get; private set;
	}
	
	public bool Loop
	{
		get; private set;
	}

	private float _volume = 1.0f;
	public float Volume 
	{
		set { _volume = value; AVProQuickTimePlugin.SetVolume(_movieHandle, _volume); }
		get { return _volume; }
	}

	public float PlaybackRate
	{
		set { AVProQuickTimePlugin.SetPlaybackRate(_movieHandle, value); }
		get { return AVProQuickTimePlugin.GetPlaybackRate(_movieHandle); }
	}
	
	public uint Frame
	{
		get { return AVProQuickTimePlugin.GetCurrentFrame(_movieHandle); }
		set { AVProQuickTimePlugin.SeekFrame(_movieHandle, value); }
	}

	public float PositionSeconds
	{
		get { return AVProQuickTimePlugin.GetCurrentPositionSeconds(_movieHandle); }
		set { if (value <= LoadedSeconds) AVProQuickTimePlugin.SeekSeconds(_movieHandle, value); }
	}

	// Display

	public Texture OutputTexture
	{
		get { if (_formatConverter != null && _formatConverter.ValidPicture) return _formatConverter.OutputTexture; return null; }
	}
	
	public int DisplayFrame
	{
		get { if (_formatConverter != null && _formatConverter.ValidPicture) return _formatConverter.DisplayFrame; return -1; }
	}
	
	
	//-------------------------------------------------------------------------
	
	public bool StartFromFile(string filename, bool loop, bool allowYUV, bool yuvHD)
	{
		Close();
		
		Filename = filename = filename.Trim();
		
		// If we're running outside of the editor we may need to resolve the relative path
		// as the working-directory may not be that of the application EXE.
		if (!Application.isEditor && !Path.IsPathRooted(filename))
		{
			string rootPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
			Filename = Path.Combine(rootPath, filename);
		}
		
		_movieSource = AVProQuickTimePlugin.MovieSource.LocalFile;
		_yuvHD = yuvHD;

		return StartMovie(loop, allowYUV);
	}

	public bool StartFromURL(string url, bool loop, bool allowYUV, bool yuvHD)
	{
		Close();

		Filename = url.Trim();
		_movieSource = AVProQuickTimePlugin.MovieSource.URL;
		_yuvHD = yuvHD;

		return StartMovie(loop, allowYUV);
	}

	public bool StartFromMemory(byte[] movieData, string filename, bool loop, bool allowYUV, bool yuvHD)
	{
		Dispose();
		Close();
		
		if (movieData == null || movieData.Length < 8)
			return false;

		Filename = filename.Trim();
		_movieSource = AVProQuickTimePlugin.MovieSource.Memory;
		_movieMemoryHandle = GCHandle.Alloc(movieData, GCHandleType.Pinned);
		_movieMemoryPtr = _movieMemoryHandle.AddrOfPinnedObject();
		_movieMemoryLength = (UInt32)(movieData.Length);
		_yuvHD = yuvHD;

		return StartMovie(loop, allowYUV);
	}

	private bool StartMovie(bool loop, bool allowYUV)
	{
		Loop = loop;

		if (_movieHandle < 0)
		{
			_movieHandle = AVProQuickTimePlugin.GetInstanceHandle();
		}
		
#if AVPRO_UNITY_4_X
		AVProQuickTimePlugin.SetActive(_movieHandle, IsActive);
#endif
		
		bool sourceValid = false;
		bool loadedOK = false;
		switch (_movieSource)
		{
		case AVProQuickTimePlugin.MovieSource.LocalFile:
			if (!string.IsNullOrEmpty(Filename))
			{
				sourceValid = true;
							
				byte[] utf8 = Encoding.UTF8.GetBytes(Filename);
				
				int size = Marshal.SizeOf(typeof(byte)) * (utf8.Length + 1);
				IntPtr ptr = Marshal.AllocHGlobal(size);
 				Marshal.Copy(utf8, 0, ptr, utf8.Length);
				Marshal.WriteByte(ptr, utf8.Length, 0);
				
				loadedOK = AVProQuickTimePlugin.LoadMovieFromFile(_movieHandle, ptr, Loop, allowYUV);
				
				Marshal.FreeHGlobal(ptr);
			}
			break;
		case AVProQuickTimePlugin.MovieSource.URL:
			if (!string.IsNullOrEmpty(Filename))
			{
				sourceValid = true;
				loadedOK = AVProQuickTimePlugin.LoadMovieFromURL(_movieHandle, Filename, Loop, allowYUV);
			}
			break;
		case AVProQuickTimePlugin.MovieSource.Memory:
			sourceValid = true;
			loadedOK = AVProQuickTimePlugin.LoadMovieFromMemory(_movieHandle, _movieMemoryPtr, _movieMemoryLength, Loop, allowYUV);
			break;
		}
		
		if (sourceValid)
		{
			if (loadedOK)
			{
				PlayState = PlaybackState.Loading;
			}
			else
			{
				Debug.LogWarning("[AVProQuickTime] Movie failed to load");
				Close();
			}
		}
		else
		{
			Debug.LogWarning("[AVProQuickTime] Invalid movie file specified");
			Close();
		}

		return _movieHandle >= 0;
	}

	private bool PrepareMovie()
	{
		if (_movieSource != AVProQuickTimePlugin.MovieSource.Memory)
		{
			if (!AVProQuickTimePlugin.LoadMovieProperties(_movieHandle))
			{
				Debug.LogWarning("[AVProQuickTime] Failed loading movie properties");
				Close();
				return false;
			}
		}
					
		AVProQuickTimePlugin.SetVolume(_movieHandle, _volume);
		Width = AVProQuickTimePlugin.GetWidth(_movieHandle);
		Height = AVProQuickTimePlugin.GetHeight(_movieHandle);
		FrameCount = (uint)(AVProQuickTimePlugin.GetFrameCount(_movieHandle));
		DurationSeconds = AVProQuickTimePlugin.GetDurationSeconds(_movieHandle);
		FrameRate = AVProQuickTimePlugin.GetFrameRate(_movieHandle);
		PixelFormat = (AVProQuickTimePlugin.PixelFormat)AVProQuickTimePlugin.GetFramePixelFormat(_movieHandle);
		
		IsPrepared = true;
		PlayState = PlaybackState.Loaded;
		Debug.Log("[AVProQuickTime] loaded movie " + Filename + "[" + Width + "x" + Height + " @ " + FrameRate + "hz] " + PixelFormat.ToString() + " " + DurationSeconds + " sec " + FrameCount + " frames");
			
		// Movie may not be visual, it could be audio so check width and height
		if (Width > 0 && Height > 0)
		{
			if (Width <= 4096 && Height <= 4096)
			{
				if (PixelFormat != AVProQuickTimePlugin.PixelFormat.Unknown)
				{
					IsVisual = true;
	
					if (_formatConverter == null)
					{
						_formatConverter = new AVProQuickTimeFormatConverter();
					}
					if (!_formatConverter.Build(_movieHandle, Width, Height, PixelFormat, _yuvHD, false, true))
					{
						Debug.LogWarning("[AVProQuickTime] unable to convert video format");
						Width = Height = 0;
						if (_formatConverter != null)
						{
							_formatConverter.Dispose();
							_formatConverter = null;
						}
					}
				}
				else
				{
					Debug.LogWarning("[AVProQuickTime] unknown video format");
					Width = Height = 0;
					if (_formatConverter != null)
					{
						_formatConverter.Dispose();
						_formatConverter = null;
					}
				}
			}
			else
			{
				Debug.LogError("[AVProQuickTime] Movie resolution is too large");
				Width = Height = 0;
				if (_formatConverter != null)
				{
					_formatConverter.Dispose();
					_formatConverter = null;
				}
			}
		}
		else
		{
			// No video frame found, must be audio?
			Width = Height = 0;
			if (_formatConverter != null)
			{
				_formatConverter.Dispose();
				_formatConverter = null;
			}
		}

		return true;
	}

	public bool Update(bool force)
	{
		bool updated = false;
		if (_movieHandle >= 0)
		{
			// Check for failing of asynchronous movie loading
			if (!AVProQuickTimePlugin.IsMovieLoadable(_movieHandle))
			{
				Debug.LogWarning("[AVProQuickTime] Unable to load movie: " + Filename);
				Close();
				return false;
			}

			if (IsPrepared)
			{
				AVProQuickTimePlugin.Update(_movieHandle);
				
				//if (!IsPaused)
				{	
					bool nextFrameReady = false;
				
					if (AVProQuickTimeManager.Instance.TextureConversionMethod == AVProQuickTimeManager.ConversionMethod.Unity4 ||
						AVProQuickTimeManager.Instance.TextureConversionMethod == AVProQuickTimeManager.ConversionMethod.Unity35_OpenGL)
					{
						nextFrameReady = true;
					}
					else
					{
						/*if (!force && IsVisual)
						{
							//nextFrameReady = AVProQuickTimePlugin.IsNextFrameReadyForGrab(_movieHandle);
						}*/

						uint qtFrame = AVProQuickTimePlugin.GetNumFramesDrawn(_movieHandle);
						if (_lastFrameDrawn != qtFrame)
						{
							nextFrameReady = true;
							_lastFrameDrawn = qtFrame;
						}

					}
					if (nextFrameReady && IsVisual)
					{
						updated = true;
						if (_formatConverter != null)
						{
							_formatConverter.Update();
						}
					}
				}
			}
			else
			{
				if (AVProQuickTimePlugin.IsMoviePropertiesLoaded(_movieHandle))
				{
					PrepareMovie();
				}
			}
		}

		return updated;
	}

	public void Play()
	{
		if (_movieHandle >= 0 && IsPaused)
		{
			AVProQuickTimePlugin.Play(_movieHandle);
			IsPaused = false;
			PlayState = PlaybackState.Playing;
			IsPlaying = true;
		}
	}

	public void Pause()
	{
		if (_movieHandle >= 0 && !IsPaused)
		{
			AVProQuickTimePlugin.Stop(_movieHandle);
			IsPaused = true;
			PlayState = PlaybackState.Stopped;
			IsPlaying = false;
		}
	}

	public void Dispose()
	{
		Close();
		if (_formatConverter != null)
		{
			_formatConverter.Dispose();
			_formatConverter = null;
		}
	}
	
	public void Close()
	{
		Pause();
		
		IsVisual = false;
		Width = Height = 0;
		PixelFormat = AVProQuickTimePlugin.PixelFormat.Unknown;
		IsPrepared = false;
		IsPaused = true;
		IsPlaying = false;
		PlayState = PlaybackState.Unknown;
		_lastFrameDrawn = 0xffffffff;
		
		if (_formatConverter != null)
		{
			_formatConverter.Reset();
		}

		if (_movieMemoryHandle.IsAllocated)
			_movieMemoryHandle.Free();
		_movieMemoryPtr = IntPtr.Zero;
		_movieMemoryLength = 0;
		
		if (_movieHandle >= 0)
		{
			AVProQuickTimePlugin.FreeInstanceHandle(_movieHandle);
			_movieHandle = -1;
		}
	}
}