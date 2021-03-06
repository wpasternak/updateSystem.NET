﻿/**
 * updateSystem.NET
 * Copyright (c) 2008-2012 Maximilian Krauss <http://coffeeInjection.com> eMail: max@coffeeInjection.com
 *
 * This library is licened under The Code Project Open License (CPOL) 1.02
 * which can be found online at <http://www.codeproject.com/info/cpol10.aspx>
 * 
 * THIS WORK IS PROVIDED "AS IS", "WHERE IS" AND "AS AVAILABLE", WITHOUT
 * ANY EXPRESS OR IMPLIED WARRANTIES OR CONDITIONS OR GUARANTEES. YOU,
 * THE USER, ASSUME ALL RISK IN ITS USE, INCLUDING COPYRIGHT INFRINGEMENT,
 * PATENT INFRINGEMENT, SUITABILITY, ETC. AUTHOR EXPRESSLY DISCLAIMS ALL
 * EXPRESS, IMPLIED OR STATUTORY WARRANTIES OR CONDITIONS, INCLUDING
 * WITHOUT LIMITATION, WARRANTIES OR CONDITIONS OF MERCHANTABILITY,
 * MERCHANTABLE QUALITY OR FITNESS FOR A PARTICULAR PURPOSE, OR ANY
 * WARRANTY OF TITLE OR NON-INFRINGEMENT, OR THAT THE WORK (OR ANY
 * PORTION THEREOF) IS CORRECT, USEFUL, BUG-FREE OR FREE OF VIRUSES.
 * YOU MUST PASS THIS DISCLAIMER ON WHENEVER YOU DISTRIBUTE THE WORK OR
 * DERIVATIVE WORKS.
 */
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace updateSystemDotNet.Administration.UI.Controls {
	internal sealed class explorerTreeView : TreeView {
		#region InsertType enum

		public enum InsertType {
			BeforeNode,
			AfterNode,
			InsideNode
		}

		#endregion

		private long m_Ticks;
		private TreeNode pseudoSelectedNode;
		private TVITEM tempTVItem;

		public explorerTreeView() {
			base.SetStyle(ControlStyles.Opaque, true);
			BorderStyle = BorderStyle.None;
		}

		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		private static extern IntPtr SendMessage(IntPtr hWnd, [MarshalAs(UnmanagedType.U4)] int msg, IntPtr wParam,
		                                         ref TVITEM item);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, bool wParam, IntPtr lParam);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		private static extern int SendMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);

		[DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
		private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);

		[DllImport("user32.dll")]
		private static extern IntPtr BeginPaint(IntPtr hWnd, ref PAINTSTRUCT paintStruct);

		[DllImport("user32.dll")]
		private static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT paintStruct);

		protected override void OnPaint(PaintEventArgs e) {
			base.OnPaint(e);

			e.Graphics.DrawLine(
				SystemPens.ControlLight,
				new Point(e.ClipRectangle.Width - 1, 0),
				new Point(e.ClipRectangle.Width - 1, e.ClipRectangle.Height)
				);
		}

		private void PseudoSelectNode(TreeNode node, bool selected) {
			tempTVItem.hItem = node.Handle;
			tempTVItem.state = selected ? 2 : 0;
			SendMessage(base.Handle, 0x113f, new IntPtr(0), ref tempTVItem);
		}

		public void SetInsertionMark(TreeNode Node, InsertType insertPostion) {
			tempTVItem.mask = 8;
			tempTVItem.stateMask = 2;
			if (pseudoSelectedNode != null) {
				PseudoSelectNode(pseudoSelectedNode, false);
				pseudoSelectedNode = null;
			}
			if (insertPostion == InsertType.InsideNode) {
				PseudoSelectNode(Node, true);
				pseudoSelectedNode = Node;
			}
			SendMessage(base.Handle, 0x111a, insertPostion == InsertType.AfterNode,
			            ((Node == null) || (insertPostion == InsertType.InsideNode)) ? IntPtr.Zero : Node.Handle);
		}

		protected override void WndProc(ref Message message) {
			switch (message.Msg) {
				case 15: {
					var paintStruct = new PAINTSTRUCT();
					IntPtr targetDC = BeginPaint(message.HWnd, ref paintStruct);
					var rectangle = new Rectangle(paintStruct.rcPaint_left, paintStruct.rcPaint_top,
					                              paintStruct.rcPaint_right - paintStruct.rcPaint_left,
					                              paintStruct.rcPaint_bottom - paintStruct.rcPaint_top);
					if ((rectangle.Width > 0) && (rectangle.Height > 0)) {
						using (BufferedGraphics graphics = BufferedGraphicsManager.Current.Allocate(targetDC, base.ClientRectangle)) {
							IntPtr hdc = graphics.Graphics.GetHdc();
							Message m = Message.Create(base.Handle, 0x318, hdc, IntPtr.Zero);
							DefWndProc(ref m);
							graphics.Graphics.ReleaseHdc(hdc);
							graphics.Render();
						}
					}
					EndPaint(message.HWnd, ref paintStruct);
					message.Result = IntPtr.Zero;
					return;
				}
				case 20:
					message.Result = (IntPtr) 1;
					return;

					/*case 0x20:
					LinkLabel2.SetCursor(LinkLabel2.LoadCursor(0, 0x7f00));
					message.Result = IntPtr.Zero;
					return;*/
			}
			base.WndProc(ref message);
		}

		protected override void OnHandleCreated(EventArgs e) {
			base.OnHandleCreated(e);
			if (Environment.OSVersion.Version.Major >= 6) {
				base.ShowLines = false;
				base.HotTracking = true;
				int lParam = SendMessage(base.Handle, 0x112d, 0, 0) | 0x40;
				SendMessage(base.Handle, 0x112c, 0, lParam);
				SetWindowTheme(base.Handle, "explorer", null);
			}
			else {
				base.HotTracking = false;
			}
		}

		protected override void OnDragOver(DragEventArgs drgevent) {
			base.OnDragOver(drgevent);
			Point pt = base.PointToClient(new Point(drgevent.X, drgevent.Y));
			TreeNode nodeAt = base.GetNodeAt(pt);
			var span = new TimeSpan(DateTime.Now.Ticks - m_Ticks);
			if (pt.Y < base.ItemHeight) {
				if (nodeAt.PrevVisibleNode != null) {
					nodeAt = nodeAt.PrevVisibleNode;
				}
				nodeAt.EnsureVisible();
				m_Ticks = DateTime.Now.Ticks;
			}
			else if ((pt.Y < (base.ItemHeight*2)) && (span.TotalMilliseconds > 250.0)) {
				nodeAt = nodeAt.PrevVisibleNode;
				if (nodeAt.PrevVisibleNode != null) {
					nodeAt = nodeAt.PrevVisibleNode;
				}
				nodeAt.EnsureVisible();
				m_Ticks = DateTime.Now.Ticks;
			}
			if (pt.Y > base.ItemHeight) {
				if (nodeAt.NextVisibleNode != null) {
					nodeAt = nodeAt.NextVisibleNode;
				}
				nodeAt.EnsureVisible();
				m_Ticks = DateTime.Now.Ticks;
			}
			else if ((pt.Y > (base.ItemHeight*2)) && (span.TotalMilliseconds > 250.0)) {
				nodeAt = nodeAt.NextVisibleNode;
				if (nodeAt.NextVisibleNode != null) {
					nodeAt = nodeAt.NextVisibleNode;
				}
				nodeAt.EnsureVisible();
				m_Ticks = DateTime.Now.Ticks;
			}
		}

		#region Nested type: PAINTSTRUCT

		[StructLayout(LayoutKind.Sequential)]
		private struct PAINTSTRUCT {
			public readonly IntPtr hdc;
			public readonly bool fErase;
			public readonly int rcPaint_left;
			public readonly int rcPaint_top;
			public readonly int rcPaint_right;
			public readonly int rcPaint_bottom;
			public readonly bool fRestore;
			public readonly bool fIncUpdate;
			public readonly int reserved1;
			public readonly int reserved2;
			public readonly int reserved3;
			public readonly int reserved4;
			public readonly int reserved5;
			public readonly int reserved6;
			public readonly int reserved7;
			public readonly int reserved8;
		}

		#endregion

		#region Nested type: TVITEM

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
		private struct TVITEM {
			public int mask;
			public IntPtr hItem;
			public int state;
			public int stateMask;
			public readonly IntPtr pszText;
			public readonly IntPtr cchTextMax;
			public readonly int iImage;
			public readonly int iSelectedImage;
			public readonly int cChildren;
			public readonly int lParam;
		}

		#endregion
	}
}