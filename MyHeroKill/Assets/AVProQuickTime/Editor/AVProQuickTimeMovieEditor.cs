using UnityEngine;
using UnityEditor;
using System.Collections;

//-----------------------------------------------------------------------------
// Copyright 2012 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

[CustomEditor(typeof(AVProQuickTimeMovie))]
public class AVProQuickTimeMovieEditor : Editor
{
	private AVProQuickTimeMovie _movie;
	
	public override void OnInspectorGUI()
	{
		_movie = (this.target) as AVProQuickTimeMovie;


		//DrawDefaultInspector();
		_movie._folder = EditorGUILayout.TextField("Folder", _movie._folder);
		_movie._filename = EditorGUILayout.TextField("Filename", _movie._filename);

		EditorGUILayout.BeginHorizontal();
		EditorGUILayout.PrefixLabel("Source");
		_movie._source = (AVProQuickTimePlugin.MovieSource)EditorGUILayout.EnumPopup(_movie._source);
		EditorGUILayout.EndHorizontal();

		_movie._loop = EditorGUILayout.Toggle("Loop", _movie._loop);
		_movie._loadOnStart = EditorGUILayout.Toggle("Load On Start", _movie._loadOnStart);
		_movie._playOnStart = EditorGUILayout.Toggle("Play On Start", _movie._playOnStart);
		//_movie._loadFirstFrame = EditorGUILayout.Toggle("Load First Frame", _movie._loadFirstFrame);
		
		EditorGUILayout.Separator();
		_movie._allowYUV = EditorGUILayout.Toggle("Allow YUV", _movie._allowYUV);
		if (_movie._allowYUV)
		{
			_movie._useYUVHD = EditorGUILayout.Toggle("Use YUV Rec709", _movie._useYUVHD);
		}
		EditorGUILayout.Separator();
		
		EditorGUILayout.BeginHorizontal();
		EditorGUILayout.PrefixLabel("Audio Volume");
		_movie._volume = EditorGUILayout.Slider(_movie._volume, 0.0f, 1.0f);
		EditorGUILayout.EndHorizontal();


		GUILayout.Space(8.0f);

		AVProQuickTime media = _movie.MovieInstance;

		_movie._editorPreview = EditorGUILayout.Foldout(_movie._editorPreview, "Video Preview");

		if (_movie._editorPreview)
		{
			{
				Rect textureRect = GUILayoutUtility.GetRect(64.0f, 64.0f, GUILayout.MinWidth(64.0f), GUILayout.MinHeight(64.0f));
				Texture texture = _movie.OutputTexture;
				if (texture == null)
					texture = EditorGUIUtility.whiteTexture;
				GUI.DrawTexture(textureRect, texture, ScaleMode.ScaleToFit);

				if (Application.isPlaying && media != null)
				{
					GUILayout.Label(string.Format("{0}x{1} @ {2}fps {3} secs", media.Width, media.Height, media.FrameRate.ToString("F2"), media.DurationSeconds.ToString("F2")));
				}
			}

			if (Application.isPlaying)
			{
				if (media != null)
				{
					GUILayout.Space(8.0f);
					
					//EditorGUILayout.LabelField("Drawn:" + AVProQuickTimePlugin.GetNumFramesDrawn(_movie.MovieInstance.Handle));

					EditorGUILayout.LabelField("Frame:");
					uint currentFrame = media.Frame;
					int newFrame = EditorGUILayout.IntSlider((int)currentFrame, 0, (int)media.FrameCount);
					if (newFrame != currentFrame)
					{
						media.Frame = (uint)newFrame;
					}
					
					GUILayout.Label("Running at " + _movie._fps.ToString("F1") + " fps");
					
					if (!media.IsPlaying)
					{
						if (GUILayout.Button("Unpause Stream"))
						{
							_movie.Play();
						}
					}
					else
					{
						if (GUILayout.Button("Pause Stream"))
						{
							_movie.Pause();
						}
					}

					if (_movie._editorPreview && media.IsPlaying)
					{
						this.Repaint();
					}
				}
			}
		}
	}
}