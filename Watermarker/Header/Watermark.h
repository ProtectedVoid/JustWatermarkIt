#pragma once
class Watermark
{
public:
	int ProcessImages(int argc, char** argv);

private:
	std::string wing(std::string path);
	cv::Mat getImage(const char* path);
	char* clone(char* src);

};

