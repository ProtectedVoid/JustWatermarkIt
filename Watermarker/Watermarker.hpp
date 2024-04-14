#pragma once

#include <vector>
#include <opencv2/core.hpp>

class Watermarker
{
public:
	static int ProcessImages(char* watermark_file, char* watermark_mask, std::vector<std::string> pictures);
	static const std::string TO_WATERMARK_FOLDER;

private :
	static std::string wing(std::string path);
	static cv::Mat getImage(std::string path);
	static void printFeedback(int current, int total);
};