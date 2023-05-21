(function () {
    "use strict";

    function AstaPhotoGalleryListViewController($scope, listViewHelper, $location, mediaResource, mediaHelper, imageUrlGeneratorResource) {

        var vm = this;

        vm.selectItem = selectItem;
        vm.clickItem = clickItem;

        // Init the controller
        function activate() {

            // Load background image for each item
            angular.forEach($scope.items, function (item) {
                getBackgroundImage(item);
            });

        }

        // Load background image
        function getBackgroundImage(item) {
            var mediaObj = eval(item.photo);
            mediaResource.getById(mediaObj[0].mediaKey)
                .then(function (media) {
                    // find the image thumbnail
                    imageUrlGeneratorResource.getCropUrl(media.mediaLink, 150, 150).then(function (cropUrl) {
                        item.imageThumbnail = cropUrl;
                    });
                });
        }

        // Item select handler
        function selectItem(selectedItem, $event, index) {

            // use the list view helper to select the item
            listViewHelper.selectHandler(selectedItem, index, $scope.items, $scope.selection, $event);
            $event.stopPropagation();

        }

        // Item click handler
        function clickItem(item) {

            // change path to edit item
            $location.path($scope.entityType + '/' + $scope.entityType + '/edit/' + item.id);

        }

        activate();

    }

    angular.module("umbraco").controller("My.ListView.Layout.AstaPhotoGalleryListViewController", AstaPhotoGalleryListViewController);

})();