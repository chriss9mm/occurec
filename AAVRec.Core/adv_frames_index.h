#ifndef ADVFRAMESINDEX_H
#define ADVFRAMESINDEX_H

#include <vector>
#include <stdio.h>

using namespace std;

namespace AavLib
{
		
class IndexEntry
{
	public:
		unsigned int ElapsedTime;
		__int64 FrameOffset;
		unsigned int  BytesCount;
};

class AavFramesIndex {

	private:
		vector<IndexEntry*>* m_IndexEntries;
	
	public:
		AavFramesIndex();
		~AavFramesIndex();

		void AddFrame(unsigned int frameNo, unsigned int elapedTime, __int64 frameOffset, unsigned int  bytesCount);
		void WriteIndex(FILE *file);
};

}

#endif // ADVFRAMESINDEX_H
