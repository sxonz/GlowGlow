#ifndef GLOWGLOW_BARRAGE_VISIBILITY
#define GLOWGLOW_BARRAGE_VISIBILITY
float _BarrageOwner;
float _LocalBarrageOwner;
float _OwnBarrageOpacity;
float OwnBarrageOpacity()
{
    return _BarrageOwner > .5 && abs(_BarrageOwner - _LocalBarrageOwner) < .25 ? _OwnBarrageOpacity : 1;
}
#endif
