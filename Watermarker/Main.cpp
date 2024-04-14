#include <iostream>
#include <sys/types.h>
#include <filesystem>
#include "Watermarker.hpp"

namespace fs = std::filesystem;
using namespace std;

int main(int argc, char** argv)
{
	if (argc == 1)
	{
		if (!fs::exists(Watermarker::TO_WATERMARK_FOLDER))
		{
			fs::create_directory(Watermarker::TO_WATERMARK_FOLDER);
		}
		cout << "Directory structure has been created." << endl;
		cout << "Place your images in the './to_watermark' folder." << endl;
		cout << "Then restart the executable with './Watermarker <destination_folder> <watermark_path> <watermark_mask>'" << endl;
		system("pause");
		return 0;
	}
	else if(argc != 4)
	{
		cout << "Usage : " << argv[0] << " <destination_folder> <watermark_path> <watermark_mask>" << endl;
		return -1;
	}

	char* targetDirectory = argv[1];
	string targetRaw = string(targetDirectory) + "\\raw";

	if (!fs::exists(targetDirectory) || !fs::is_directory(targetDirectory))
	{
		cout << "Target folder does not exists or is invalid." << endl;
		system("pause");
		exit(-1);
	}
	else if (!fs::exists(targetRaw))
	{
		fs::create_directory(targetRaw);
	}

	char* watermark = argv[2];
	char* mask = argv[3];
	vector<string> images;

	for (const auto& entry : fs::directory_iterator(".\\to_watermark"))
	{
		images.push_back(entry.path().string());
	}

	Watermarker::ProcessImages(watermark, mask, images);

	cout << "Moving pictures" << endl;

	for (const auto& entry : fs::directory_iterator(".\\to_watermark"))
	{
		if (entry.path().string().find("\\w_") == string::npos)
		{
			fs::copy(entry.path(), targetRaw);
		}
		else
		{
			fs::copy(entry.path(), targetDirectory);
		}
		fs::remove(entry.path());
	}

	cout << "Done !" << endl;
	system("pause");

	return 0;
}