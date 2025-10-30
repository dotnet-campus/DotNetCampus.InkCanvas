﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using WpfInk.PresentationCore.System.Windows;

namespace WpfInk;

internal interface IInternalStreamGeometryContext
{
    void BeginFigure(Point startPoint, bool isFilled, bool isClosed);
    void PolyBezierTo(IList<Point> points, bool isStroked, bool isSmoothJoin);
    void PolyLineTo(IList<Point> points, bool isStroked, bool isSmoothJoin);
    void ArcTo(Point point, Size size, double rotationAngle, bool isLargeArc, bool sweepDirection, bool isStroked, bool isSmoothJoin);
}