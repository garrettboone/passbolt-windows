/**
 * Passbolt ~ Open source password manager for teams
 * Copyright (c) Passbolt SA (https://www.passbolt.com)
 *
 * Licensed under GNU Affero General Public License version 3 of the or any later version.
 * For full copyright and license information, please see the LICENSE.txt
 * Redistributions of files must retain the above copyright notice.
 *
 * @copyright     Copyright (c) Passbolt SA (https://www.passbolt.com)
 * @license       https://opensource.org/licenses/AGPL-3.0 AGPL License
 * @link          https://www.passbolt.com Passbolt(tm)
 * @since         0.0.1
 */

import { INITIALIZATION } from "../enumerations/appEventEnumeration";

class AppEvent {
    onMessageReceived(ipc) {
        switch (ipc.topic) {
            case INITIALIZATION:
                window.chrome.webview.postMessage(JSON.stringify({ topic: INITIALIZATION }));
                break;
        }
    }
}


export default AppEvent;
